using Desertod.Domain.Buildings;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record CivicNotice(Guid OrderId, string Slug, string OwnerId, string RequestedBy,
    string Operation, string BuildingKind, string PlotId, int? BuildingNumber = null, int? DisplayNumber = null);
public sealed record CivicTelegramVote(Guid OrderId, string Slug, string OwnerId, bool Approve);

public sealed partial class GameService
{
    // Существующие pending-заявки тоже попадают в очередь. Сеть вне блокировки деревни.
    public Task<CivicNotice?> ClaimCivicNoticeAsync(Guid villageId, CancellationToken ct) =>
        WithVillageAsync<CivicNotice?>(villageId, async db => {
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, ct);
            var now = time.GetUtcNow();
            var order = village.CivicOrders.Where(o => o.Status == "pending" && o.RequestedBy != village.OwnerAccountId
                && o.Operation is "build" or "upgrade" or "demolish")
                .Where(o => o.NoticeState == "Queued" || o.NoticeState == "Unavailable" && o.NoticeAt <= now.AddMinutes(-5)
                    || o.NoticeState == "Sending" && o.NoticeAt <= now.AddMinutes(-2))
                .OrderBy(o => o.CreatedAt).FirstOrDefault();
            if (order is null) return null;
            var owner = await db.ExternalLogins.Where(l => l.AccountId == village.OwnerAccountId && l.Provider == "telegram")
                .Select(l => l.ProviderUserId).SingleOrDefaultAsync(ct);
            if (owner is null) return null;
            var author = await db.Accounts.Where(a => a.Id == order.RequestedBy).Select(a => a.DisplayName).SingleOrDefaultAsync(ct) ?? "Посадник";
            var kind = order.BuildingKind ?? village.Structures.SingleOrDefault(s => s.PlotId == order.PlotId)?.Kind
                ?? (village.Storages.SingleOrDefault(s => s.PlotId == order.PlotId) is {} storage ? BuildingCatalog.ForStorage(storage.Kind) : null);
            order.NoticeState = "Sending"; order.NoticeAt = now;
            await db.SaveChangesAsync(ct);
            var number=village.Structures.FirstOrDefault(s=>s.PlotId==order.PlotId)?.Number
                ??village.Storages.FirstOrDefault(s=>s.PlotId==order.PlotId)?.Number;
            return new(order.Id, village.Slug, owner, author, order.Operation, kind?.ToString() ?? "Building", order.PlotId,
                number,order.BuildingKind==BuildingKind.FortificationUpgrade?0:number is {} n?BuildingLabels.Visible(village).GetValueOrDefault(n):null);
        }, ct);

    public Task<bool> CompleteCivicNoticeAsync(Guid villageId, Guid orderId, string state, CancellationToken ct) =>
        WithVillageAsync(villageId, async db => {
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, ct);
            var order = village.CivicOrders.SingleOrDefault(o => o.Id == orderId);
            if (order?.NoticeState != "Sending") return false;
            order.NoticeState = state is "Sent" or "Unknown" or "Unavailable" ? state : "Unknown";
            order.NoticeAt = time.GetUtcNow(); await db.SaveChangesAsync(ct); return true;
        }, ct);
}

public sealed class CivicNotificationService(GameService game, IDbContextFactory<DesertodDbContext> factory,
    IAlmightyBridge bridge, IConfiguration config, ILogger<CivicNotificationService> logger)
{
    public async Task TickAsync(CancellationToken ct)
    {
        var primary = config["Hosting:PrimaryVillage"];
        if (string.IsNullOrWhiteSpace(primary)) return;
        await using var db = await factory.CreateDbContextAsync(ct);
        var id = await db.Villages.Where(v => v.Slug == primary).Select(v => v.Id).SingleOrDefaultAsync(ct);
        if (id == Guid.Empty) return;
        var notice = await game.ClaimCivicNoticeAsync(id, ct);
        if (notice is null) return;
        var state = "Unavailable";
        try { state = (await bridge.NotifyCivicAsync(notice, ct)).State; }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        { logger.LogWarning("Civic notification bridge unavailable ({Type}).", e.GetType().Name); }
        // Повтор использует тот же OrderId: бот хранит квитанцию до отправки в Telegram.
        await game.CompleteCivicNoticeAsync(id, notice.OrderId, state, ct);
    }
}

public sealed class CivicNotificationWorker(CivicNotificationService notices, TimeProvider time,
    ILogger<CivicNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            try { await notices.TickAsync(stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException)
            { logger.LogWarning("Civic notification tick failed ({Type}).", e.GetType().Name); }
    }
}
