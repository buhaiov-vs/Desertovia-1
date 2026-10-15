using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record EconomyRequest(Guid RequestId, string Action, ResourceKind Resource = ResourceKind.Wood,
    int Amount = 1, long Price = 0, Guid? Target = null, int X = 0, int Y = 0, BuildingKind Building = BuildingKind.Hut);
public sealed record MarketLotDto(Guid Id, Guid SellerAccountId, ResourceKind Resource, int Amount, long Price, string Status, string Seller);
public sealed record PersonalGoodDto(ResourceKind Kind, int Amount, long CostBasis);
public sealed record LedgerDto(Guid Id, DateTimeOffset At, string Operation, string Asset, string Location, long Delta, string? Actor);
public sealed record EconomyView(long Coins, long TestCandles, long EarnedToday, long ProfitToday, long TaxPreview,
    bool TaxEnabled, int ExchangeRate, int DailyExchangeLimit, int ExchangedToday, int KingdomCandleRoom,
    DateTimeOffset DayResetsAt, long Treasury, IReadOnlyList<PersonalGoodDto> Inventory, IReadOnlyList<MarketLotDto> Lots,
    IReadOnlyList<LedgerDto> Journal, Guid? NextJournalBefore, DateTimeOffset StartedAt,
    int WorkPay, int ConstructionPay, IReadOnlyList<EconomyResident> Residents, IReadOnlyList<PrivateStorageDto> PrivateStorages, IReadOnlyDictionary<ResourceKind, int> StockPrices, IReadOnlyList<PrivatePropertyDto> PrivateProperty);
public sealed record PrivatePropertyDto(string PlotId, BuildingKind Kind, int Level, bool Site, bool HasWork);
public sealed record EconomyResident(Guid AccountId, string Name);
public sealed record PrivateStorageDto(string PlotId, StorageKind Kind, IReadOnlyList<ResourceAmount> Goods);

public sealed partial class GameService
{
    private int EconomyNumber(string key, int fallback, int min, int max) =>
        Math.Clamp(configuration.GetValue<int?>("Game:Economy:" + key) ?? fallback, min, max);

    private static DateOnly EconomyDay(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);
    private static Guid StableOperation(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    private static async Task<GameWallet> WalletAsync(DesertodDbContext db, Guid village, Guid account, DateTimeOffset now, CancellationToken ct)
    {
        var wallet = await db.GameWallets.SingleOrDefaultAsync(w => w.VillageId == village && w.AccountId == account, ct);
        if (wallet is null) { wallet = new GameWallet { VillageId = village, AccountId = account }; db.GameWallets.Add(wallet); }
        var day = EconomyDay(now);
        if (wallet.IncomeDay != day) { wallet.IncomeDay = day; wallet.EarnedToday = 0; wallet.ProfitToday = 0; }
        if (wallet.ExchangeDay != day) { wallet.ExchangeDay = day; wallet.ExchangedToday = 0; }
        return wallet;
    }

    private static void MoneyEntry(DesertodDbContext db, Guid village, Guid operation, Guid? account, string action,
        string asset, string location, long amount, DateTimeOffset now)
    {
        if (amount == 0) return;
        db.LedgerEntries.Add(new LedgerEntry { VillageId = village, OperationId = operation, AccountId = account,
            Operation = action, Asset = asset, Location = location, Delta = amount, At = now });
    }

    private async Task PayUsefulWorkAsync(DesertodDbContext db, Village village, CycleReport? report, DateTimeOffset now, CancellationToken ct)
    {
        if (report is null || report.TaskId == "dismantle") return;
        var citizen = await db.Citizens.SingleAsync(c => c.Id == report.CitizenId, ct);
        if (citizen.CyclePrivateOutput) return;
        var useful = report.ProjectContribution > 0 || report.Repaired > 0 || report.TechnologyId is not null && report.PrimaryExperience > 0
            || report.Produced.Sum(x => x.Amount) > report.Lost.Sum(x => x.Amount);
        if (!useful && !(GameCatalog.FindTask(report.TaskId)?.Effect == TaskEffect.Guard && report.PrimaryExperience > 0)) return;
        var id = StableOperation($"work:{citizen.Id}:{citizen.CycleStartedAt:O}:{report.TaskId}");
        if (await db.EconomyReceipts.AnyAsync(x => x.VillageId == village.Id && x.RequestId == id, ct)) return;
        var pay = report.ProjectContribution > 0 || report.Repaired > 0
            ? EconomyNumber("ConstructionPay", 20, 1, 1000) : EconomyNumber("WorkPay", 10, 1, 1000);
        var wallet = await WalletAsync(db, village.Id, citizen.AccountId, now, ct);
        EconomyRules.Credit(wallet, pay); wallet.EarnedToday = checked(wallet.EarnedToday + pay);
        db.EconomyReceipts.Add(new EconomyReceipt { VillageId = village.Id, RequestId = id, AccountId = citizen.AccountId, Fingerprint = "work", At = now });
        MoneyEntry(db, village.Id, id, citizen.AccountId, "wage", "DZK", "wallet", pay, now);
        MoneyEntry(db, village.Id, id, citizen.AccountId, "work-completed", "work:" + report.TaskId,
            report.SiteId is {} plot ? "site:" + plot : "village", Math.Max(1, report.ProjectContribution), now);
    }

    private async Task RequireEconomyMemberAsync(DesertodDbContext db, Village village, Guid account, CancellationToken ct)
    {
        if (village.OwnerAccountId != account && !await db.Citizens.AnyAsync(c => c.VillageId == village.Id && c.AccountId == account, ct))
            throw new UnauthorizedAccessException();
    }

    public async Task<EconomyView> GetEconomyAsync(string slug, Guid account, Guid? before, string? asset, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct);
            await RequireEconomyMemberAsync(db, village, account, ct);
            return await EconomyViewAsync(db, village, account, before, asset, ct);
        }, ct);

    public async Task<EconomyView> ChangeEconomyAsync(string slug, Guid account, EconomyRequest request, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct);
            await RequireEconomyMemberAsync(db, village, account, ct);
            if (request.RequestId == Guid.Empty || !Enum.IsDefined(request.Resource))
                throw new GameRuleException("economy.request", "Refresh the operation and try again.");
            var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
            var prior = await db.EconomyReceipts.SingleOrDefaultAsync(x => x.VillageId == village.Id && x.RequestId == request.RequestId, ct);
            if (prior is not null)
            {
                if (prior.AccountId != account || prior.Fingerprint != fingerprint)
                    throw new GameRuleException("economy.reusedRequest", "This request was already used for another operation.");
                return await EconomyViewAsync(db, village, account, null, null, ct);
            }
            var now = time.GetUtcNow();
            village.Explore(await LoadTripsAsync(db,village.Id,ct),now);
            var wallet = await WalletAsync(db, village.Id, account, now, ct);
            var state = await db.KingdomEconomies.SingleAsync(x => x.VillageId == village.Id, ct);
            var citizen = await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == account, ct);
            if (request.Amount is < 1 or > PersonalInventory.PerResource || request.Price is < 0 or > 1_000_000)
                throw new GameRuleException("economy.amount", "Choose an amount from 1 to 100 and a valid whole price.");
            Citizen Own()
            {
                var resident = citizen ?? throw new GameRuleException("economy.citizen", "A resident is required for this action.");
                if (resident.RecoveryPausedAt is not null || resident.InjuredUntil > now || resident.Recovery?.Until > now || resident.ExhaustedUntil > now || resident.DungeonRunId is not null)
                    throw new GameRuleException("inventory.unavailable", "Wait until recovery or the expedition is finished.");
                return resident;
            }
            void WalletChange(Guid who, long amount, string action) => MoneyEntry(db, village.Id, request.RequestId, who, action, "DZK", "wallet", amount, now);
            db.AuditActor = account; db.AuditOperation = request.Action;
            switch (request.Action)
            {
                case "buy-stock":
                {
                    var buyer = Own();
                    if (!StorageCatalog.IsGood(request.Resource) || village.Stock(request.Resource) < request.Amount)
                        throw new GameRuleException("inventory.stockMissing", "The common storage does not have this material.");
                    if (request.Price != 0 && request.Price != EconomyRules.StorePrice(request.Resource))
                        throw new GameRuleException("economy.priceChanged", "The price has changed. Refresh the menu.");
                    var cost = checked((long)EconomyRules.StorePrice(request.Resource) * request.Amount);
                    if (PersonalInventory.Room(buyer, request.Resource) < request.Amount)
                        throw new GameRuleException("inventory.full", "Your personal inventory is full.");
                    EconomyRules.Spend(wallet, cost); village.Take(request.Resource, request.Amount);
                    PersonalInventory.Add(buyer, request.Resource, request.Amount, cost);
                    state.TreasuryCoins = checked(state.TreasuryCoins + cost);
                    WalletChange(account, -cost, "buy-stock");
                    MoneyEntry(db, village.Id, request.RequestId, null, "buy-stock", "DZK", "treasury", cost, now);
                    break;
                }
                case "deposit-private":
                {
                    var storage = village.Storages.SingleOrDefault(s => s.PlotId == WorldMap.PlotAt(request.X, request.Y) && s.OwnerAccountId == account)
                        ?? throw new GameRuleException("building.private", "Choose your private warehouse.");
                    PrivateWarehouse.Deposit(Own(), storage, request.Resource, request.Amount);
                    break;
                }
                case "transfer":
                {
                    var sender = Own();
                    var recipient = await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == request.Target, ct)
                        ?? throw new GameRuleException("inventory.recipient", "Choose a resident in this kingdom.");
                    if (recipient.AccountId == account) throw new GameRuleException("inventory.recipient", "Choose another resident.");
                    if (PersonalInventory.Room(recipient, request.Resource) < request.Amount) throw new GameRuleException("inventory.full", "The recipient's inventory is full.");
                    var basis = PersonalInventory.Remove(sender, request.Resource, request.Amount);
                    PersonalInventory.Add(recipient, request.Resource, request.Amount, basis);
                    break;
                }
                case "list":
                {
                    if (request.Price < 1) throw new GameRuleException("economy.amount", "Choose a positive whole price for the lot.");
                    if (await db.MarketLots.CountAsync(x => x.VillageId == village.Id && x.SellerAccountId == account && x.Status == "open", ct) >= 10)
                        throw new GameRuleException("market.limit", "You already have ten open lots.");
                    var basis = PersonalInventory.Remove(Own(), request.Resource, request.Amount);
                    db.MarketLots.Add(new MarketLot { Id = request.RequestId, VillageId = village.Id, SellerAccountId = account,
                        Resource = request.Resource, Amount = request.Amount, Price = request.Price, CostBasis = basis, Status = "open", CreatedAt = now });
                    break;
                }
                case "buy-lot":
                case "cancel-lot":
                {
                    var lot = await db.MarketLots.SingleOrDefaultAsync(x => x.Id == request.Target && x.VillageId == village.Id, ct)
                        ?? throw new GameRuleException("market.missing", "This lot no longer exists.");
                    if (lot.Status != "open") throw new GameRuleException("market.closed", "This lot is already closed.");
                    var seller = await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == lot.SellerAccountId, ct)
                        ?? throw new GameRuleException("inventory.recipient", "The seller is no longer a resident.");
                    if (request.Action == "cancel-lot")
                    {
                        if (lot.SellerAccountId != account) throw new UnauthorizedAccessException();
                        PersonalInventory.Add(seller, lot.Resource, lot.Amount, lot.CostBasis); lot.Status = "cancelled";
                    }
                    else
                    {
                        if (lot.SellerAccountId == account) throw new GameRuleException("market.own", "You cannot buy your own lot.");
                        if (PersonalInventory.Room(Own(), lot.Resource) < lot.Amount) throw new GameRuleException("inventory.full", "Your personal inventory is full.");
                        var sellerWallet = await WalletAsync(db, village.Id, lot.SellerAccountId, now, ct);
                        EconomyRules.Spend(wallet, lot.Price); EconomyRules.Credit(sellerWallet, lot.Price);
                        sellerWallet.ProfitToday = checked(sellerWallet.ProfitToday + lot.Price - lot.CostBasis);
                        PersonalInventory.Add(Own(), lot.Resource, lot.Amount, lot.Price);
                        lot.Status = "sold"; lot.BuyerAccountId = account;
                        WalletChange(account, -lot.Price, "trade"); WalletChange(lot.SellerAccountId, lot.Price, "trade");
                    }
                    lot.ClosedAt = now;
                    break;
                }
                case "exchange":
                {
                    var rate = EconomyNumber("ExchangeRate", 100, 1, 100_000);
                    var limit = EconomyNumber("DailyExchangeLimit", 5, 1, 20);
                    var pool = EconomyNumber("KingdomDailyCandles", 50, 1, 1000);
                    if (state.FundDay != EconomyDay(now)) { state.FundDay = EconomyDay(now); state.CandlesIssuedToday = 0; }
                    if (wallet.ExchangedToday + request.Amount > limit || state.CandlesIssuedToday + request.Amount > pool)
                        throw new GameRuleException("exchange.limit", "The personal or kingdom daily exchange limit is reached.");
                    var cost = checked((long)rate * request.Amount);
                    EconomyRules.Spend(wallet, cost); wallet.TestCandles = checked(wallet.TestCandles + request.Amount);
                    wallet.ExchangedToday += request.Amount; state.CandlesIssuedToday += request.Amount;
                    WalletChange(account, -cost, "exchange-burn");
                    MoneyEntry(db, village.Id, request.RequestId, account, "exchange", "TEST_CANDLE", "wallet", request.Amount, now);
                    break;
                }
                case "private-upgrade":
                case "private-demolish":
                case "private-cancel":
                {
                    var owner = Own(); var plot = WorldMap.PlotAt(request.X,request.Y);
                    if(request.Action=="private-cancel")
                    {
                        if(village.Sites.SingleOrDefault(s=>s.PlotId==plot)?.OwnerAccountId!=account) throw new UnauthorizedAccessException();
                        ConstructionRules.Cancel(village,plot);
                    }
                    else
                    {
                        if(village.Structures.SingleOrDefault(s=>s.PlotId==plot)?.OwnerAccountId!=account && village.Storages.SingleOrDefault(s=>s.PlotId==plot)?.OwnerAccountId!=account)
                            throw new UnauthorizedAccessException();
                        var order=BuildingOperations.Request(village,account,plot,request.Action=="private-upgrade"?"upgrade":"demolish",now);
                        var people=await db.Citizens.Where(c=>c.VillageId==village.Id && c.Status==CitizenStatus.Active).ToListAsync(ct);
                        BuildingOperations.Approve(village,order,people,now);
                    }
                    break;
                }
                case "private-build":
                {
                    var builder = Own();
                    if (!Enum.IsDefined(request.Building) || request.Building is BuildingKind.Castle or BuildingKind.Hospital or BuildingKind.RopeBridge)
                        throw new GameRuleException("building.privateKind", "This civic building requires the Tsar's approval.");
                    var area = WorldMap.Footprint(request.X, request.Y);
                    if (KingdomRules.Area(village).Contains(area)) throw new GameRuleException("building.publicApproval", "Inside the kingdom, ask the Tsar to approve construction.");
                    var site = ConstructionRules.PlacePrivate(village, builder.AccountId, request.Building, request.X, request.Y, now);
                    db.AuditOperation = "private-build:" + site.PlotId;
                    break;
                }
                default: throw new GameRuleException("economy.action", "Unknown economic action.");
            }
            db.EconomyReceipts.Add(new EconomyReceipt { VillageId = village.Id, RequestId = request.RequestId, AccountId = account, Fingerprint = fingerprint, At = now });
            await db.SaveChangesAsync(ct); changes.Mark(village.Id);
            return await EconomyViewAsync(db, village, account, null, null, ct);
        }, ct);

    private async Task<EconomyView> EconomyViewAsync(DesertodDbContext db, Village village, Guid account, Guid? before, string? asset, CancellationToken ct)
    {
        var now = time.GetUtcNow(); var wallet = await WalletAsync(db, village.Id, account, now, ct);
        var state = await db.KingdomEconomies.SingleAsync(x => x.VillageId == village.Id, ct);
        var citizen = await db.Citizens.SingleOrDefaultAsync(c => c.VillageId == village.Id && c.AccountId == account, ct);
        var query = db.LedgerEntries.AsNoTracking().Where(x => x.VillageId == village.Id);
        if (!string.IsNullOrWhiteSpace(asset) && asset.Length <= 40) query = query.Where(x => x.Asset == asset);
        if (before is {} cursor)
        {
            var entry = await db.LedgerEntries.AsNoTracking().SingleOrDefaultAsync(x => x.VillageId == village.Id && x.Id == cursor, ct);
            if (entry is null) throw new GameRuleException("ledger.cursor", "Refresh the journal.");
            query = query.Where(x => x.At < entry.At || x.At == entry.At && x.Id.CompareTo(entry.Id) < 0);
        }
        // Personal possessions and wallet details are visible only to their owner, even in the public journal.
        query = village.OwnerAccountId == account
            ? query.Where(x => !x.Location.StartsWith("personal:") && !x.Location.StartsWith("private-storage:") && !x.Location.StartsWith("private-site:"))
            : query.Where(x => x.AccountId == null || x.AccountId == account || x.Asset != "DZK" && x.Asset != "TEST_CANDLE" && !x.Location.StartsWith("personal:") && !x.Location.StartsWith("private-storage:") && !x.Location.StartsWith("private-site:"));
        var rows = await query.OrderByDescending(x => x.At).ThenByDescending(x => x.Id).Take(51).ToListAsync(ct);
        var names = await db.Accounts.Where(a => rows.Select(x => x.AccountId).Contains(a.Id)).Select(a => new { a.Id, a.DisplayName }).ToDictionaryAsync(a => a.Id, a => a.DisplayName, ct);
        var journal = rows.Take(50).Select(x => new LedgerDto(x.Id, x.At, x.Operation, x.Asset, x.Location, x.Delta,
            x.AccountId is {} id ? names.GetValueOrDefault(id, "Resident") : null)).ToArray();
        var lots = await db.MarketLots.AsNoTracking().Where(x => x.VillageId == village.Id && (x.Status == "open" || x.SellerAccountId == account))
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        var sellers = lots.Select(x=>x.SellerAccountId).Distinct().ToArray();
        var sellerNames = await db.Accounts.Where(a=>sellers.Contains(a.Id)).Select(a=>new{a.Id,a.DisplayName}).ToDictionaryAsync(a=>a.Id,a=>a.DisplayName,ct);
        await db.SaveChangesAsync(ct);
        return new(wallet.Coins, wallet.TestCandles, wallet.EarnedToday, wallet.ProfitToday,
            EconomyRules.TaxPreview(checked(wallet.EarnedToday + wallet.ProfitToday)), false,
            EconomyNumber("ExchangeRate", 100, 1, 100_000), EconomyNumber("DailyExchangeLimit", 5, 1, 20), wallet.ExchangedToday,
            Math.Max(0, EconomyNumber("KingdomDailyCandles", 50, 1, 1000) - (state.FundDay == EconomyDay(now) ? state.CandlesIssuedToday : 0)),
            new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero), state.TreasuryCoins,
            citizen?.PersonalGoods.Select(x => new PersonalGoodDto(x.Kind, x.Amount, x.CostBasis)).ToArray() ?? [],
            lots.Select(x=>new MarketLotDto(x.Id,x.SellerAccountId,x.Resource,x.Amount,x.Price,x.Status,sellerNames.GetValueOrDefault(x.SellerAccountId,"Resident"))).ToArray(), journal, rows.Count > 50 ? rows[49].Id : null, state.StartedAt,
            EconomyNumber("WorkPay", 10, 1, 1000), EconomyNumber("ConstructionPay", 20, 1, 1000),
            await db.Citizens.AsNoTracking().Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
                .OrderBy(c => c.Name).Select(c => new EconomyResident(c.AccountId, c.Name)).ToListAsync(ct),
            village.Storages.Where(s => s.OwnerAccountId == account).Select(s => new PrivateStorageDto(s.PlotId, s.Kind, s.Goods.Select(g => new ResourceAmount(g.Kind, g.Amount)).ToArray())).ToArray(),
            Enum.GetValues<ResourceKind>().Where(StorageCatalog.IsGood).ToDictionary(k=>k,EconomyRules.StorePrice),
            village.Structures.Where(s=>s.OwnerAccountId==account).Select(s=>new PrivatePropertyDto(s.PlotId,s.Kind,s.Level,false,false))
                .Concat(village.Storages.Where(s=>s.OwnerAccountId==account).Select(s=>new PrivatePropertyDto(s.PlotId,BuildingCatalog.ForStorage(s.Kind)??BuildingKind.Heap,1,false,false)))
                .Concat(village.Sites.Where(s=>s.OwnerAccountId==account).Select(s=>new PrivatePropertyDto(s.PlotId,s.Kind,s.TargetLevel,true,s.HasWork))).ToArray());
    }
}
