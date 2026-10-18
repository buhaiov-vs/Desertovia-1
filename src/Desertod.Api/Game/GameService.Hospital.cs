using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record HospitalHealRequest(Guid PatientId, string Code, string RequestId, int ExpectedPrice, string EpisodeId);
public sealed record HospitalPatientDto(Guid Id, string Name, ExternalRecovery? Recovery, Appearance? Appearance = null);
public sealed record HospitalDto(bool Available, int Level, int? Balance, ExternalRecovery? Recovery,
    IReadOnlyList<HospitalTier> Tiers, IReadOnlyList<HospitalPatientDto> Patients);

public sealed partial class GameService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, ExternalRecovery> hospitalAccounts = new();
    public bool HasHospitalRestriction(Guid accountId) => hospitalAccounts.TryGetValue(accountId, out var r)
        && (!r.Available || r.Until > time.GetUtcNow());

    private static DateTimeOffset? Utc(double? timestamp) => timestamp is { } value
        ? DateTimeOffset.FromUnixTimeMilliseconds(checked((long)(value * 1000))) : null;

    private async Task<long?> TelegramIdAsync(DesertodDbContext db, Guid accountId, CancellationToken ct)
    {
        var id = await db.ExternalLogins.Where(l => l.AccountId == accountId && l.Provider == "telegram")
            .Select(l => l.ProviderUserId).SingleOrDefaultAsync(ct);
        return long.TryParse(id, out var value) && value > 0 ? value : null;
    }

    private async Task<HospitalStatusReply?> SyncHospitalAsync(DesertodDbContext db, Guid villageId,
        CancellationToken ct, bool force = false, Guid? actorAccount = null, bool actorOnly = false)
    {
        if (!hospitalBridge.Enabled) return null;
        // A combat click needs fresh clinical authority for its actor, not a forced village-wide sweep.
        var citizens = await db.Citizens.Where(c => c.VillageId == villageId && c.Status == CitizenStatus.Active
            && (!actorOnly || c.AccountId == actorAccount)).ToListAsync(ct);
        var village = await db.Villages.SingleAsync(v => v.Id == villageId, ct);
        var now = time.GetUtcNow();
        var ids = await db.ExternalLogins.Where(l => l.Provider == "telegram" && citizens.Select(c => c.AccountId).Contains(l.AccountId))
            .ToDictionaryAsync(l => l.AccountId, l => l.ProviderUserId, ct);
        var linked = citizens.Where(c => ids.ContainsKey(c.AccountId) && long.TryParse(ids[c.AccountId], out _)).ToList();
        HospitalStatusReply? last = null;
        var allPatients = new List<HospitalPatient>();
        var updated = new List<Citizen>();
        // The royal combat actor can have a Telegram account without a Citizen row.
        if (actorAccount is { } actorId && !linked.Any(c=>c.AccountId==actorId)
            && await TelegramIdAsync(db, actorId, ct) is { } telegram
            && (force || !hospitalAccounts.TryGetValue(actorId,out var cached) || now-cached.CheckedAt>=TimeSpan.FromSeconds(15)))
        {
            try
            {
                last=await hospitalBridge.StatusAsync(telegram,[new HospitalPatientRequest(telegram,HospitalRules.Level(village),0)],ct);
                var p=last.Patients.SingleOrDefault(p=>p.Id==telegram.ToString())??throw new HttpRequestException("Incomplete hospital reply.");
                hospitalAccounts[actorId]=new ExternalRecovery { EpisodeId=p.EpisodeId,AdmittedAt=Utc(p.AdmittedAt),Until=Utc(p.Until),CheckedAt=now,Available=true,
                    HospitalPercent=p.HospitalPercent,SkillPercent=p.SkillPercent,EffectKind=p.Effect?.Kind,EffectUntil=Utc(p.Effect?.Until) };
                allPatients.AddRange(last.Patients);
            }
            catch(Exception error) when(error is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                if(ct.IsCancellationRequested)throw;
                hospitalAccounts[actorId]=new ExternalRecovery {Available=false,CheckedAt=now};
                logger.LogWarning("Royal hospital status unavailable; combat remains blocked.");
            }
        }
        foreach (var chunk in linked.Chunk(100))
        {
            if (!force && chunk.All(c => c.Recovery is { Available: true } r && now-r.CheckedAt < TimeSpan.FromSeconds(15))) continue;
            var actor = actorAccount is { } account ? await TelegramIdAsync(db, account, ct) : await TelegramIdAsync(db, village.OwnerAccountId, ct);
            actor ??= long.Parse(ids[chunk[0].AccountId]);
            try
            {
                last = await hospitalBridge.StatusAsync(actor.Value,
                    chunk.Select(c => new HospitalPatientRequest(long.Parse(ids[c.AccountId]), HospitalRules.Level(village), HospitalRules.SkillLevel(c))).ToList(), ct);
                var byId = last.Patients.ToDictionary(p => p.Id);
                if (chunk.Any(c => !byId.ContainsKey(ids[c.AccountId]))) throw new HttpRequestException("Incomplete hospital reply.");
                allPatients.AddRange(last.Patients);
                foreach (var c in chunk)
                {
                    if (!byId.TryGetValue(ids[c.AccountId], out var p)) throw new HttpRequestException("Incomplete hospital reply.");
                    if (c.Recovery?.EpisodeId != p.EpisodeId || c.Recovery?.Until != Utc(p.Until)
                        || c.Recovery?.Available != true || c.RecoveryPausedAt is not null && p.Until is null) updated.Add(c);
                    HospitalRules.Apply(c, new ExternalRecovery { EpisodeId = p.EpisodeId,
                        AdmittedAt = Utc(p.AdmittedAt), Until = Utc(p.Until), CheckedAt = now, Available = true,
                        HospitalPercent = p.HospitalPercent, SkillPercent = p.SkillPercent, EffectKind=p.Effect?.Kind, EffectUntil=Utc(p.Effect?.Until) }, now);
                }
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                foreach (var c in chunk)
                {
                    if (c.Recovery?.Available != false) updated.Add(c);
                    var state = c.Recovery ?? new ExternalRecovery();
                    state.Available = false;
                    HospitalRules.Apply(c, state, now);
                }
                last = null;
                logger.LogWarning("Hospital bridge unavailable; external recovery remains conservative.");
            }
        }
        if (!actorOnly && linked.Any(c => c.MarriageCheckedAt is null || now-c.MarriageCheckedAt >= TimeSpan.FromSeconds(60)))
        {
            try
            {
                foreach(var chunk in linked.Chunk(100))
                {
                    var response=await hospitalBridge.RelationshipsAsync(long.Parse(ids[chunk[0].AccountId]), chunk.Select(c=>long.Parse(ids[c.AccountId])).ToArray(),ct);
                    foreach(var relation in response.Relationships)
                    {
                        var c=chunk.SingleOrDefault(p=>ids[p.AccountId]==relation.Id);if(c is null)continue;
                        c.SpouseName=relation.Name;c.MarriedSince=Utc(relation.Since);c.MarriageCheckedAt=now;
                        c.SpouseAccountId=relation.SpouseId is null ? null : await db.ExternalLogins.Where(l=>l.Provider=="telegram"&&l.ProviderUserId==relation.SpouseId)
                            .Select(l=>(Guid?)l.AccountId).SingleOrDefaultAsync(ct);
                    }
                }
            }
            catch(Exception e) when(e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            { logger.LogWarning("Marriage sync unavailable; retained last confirmed relationship."); }
        }
        await db.SaveChangesAsync(ct);
        foreach (var c in linked)
        {
            if (c.Recovery is not null) hospitalAccounts[c.AccountId] = c.Recovery;
            if (updated.Contains(c)) await notifier.CitizenUpdated(village.Slug, c.ToSummaryDto(now));
        }
        return last is null ? null : new HospitalStatusReply(allPatients, last.Balance);
    }

    public Task<bool> CheckDungeonHospitalAsync(Guid villageId, Guid accountId, CancellationToken ct) =>
        WithVillageAsync(villageId, async db =>
        {
            // Like raid orders, a dungeon click must force-check its actor only.
            // Village medical sweeps and relationships run in the periodic worker.
            await SyncHospitalAsync(db, villageId, ct, true, accountId, actorOnly: true);
            return !HasHospitalRestriction(accountId);
        }, ct);

    public async Task RefreshHospitalAsync(CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var villages = await db.Citizens.Where(c => c.Status == CitizenStatus.Active)
            .Select(c => c.VillageId).Distinct().ToListAsync(ct);
        foreach (var id in villages)
            await WithVillageAsync(id, async context =>
            {
                await SyncHospitalAsync(context,id,ct);
                var village=await context.Villages.SingleAsync(v=>v.Id==id,ct);
                await RescueSweepAsync(context,village,time.GetUtcNow(),ct);await context.SaveChangesAsync(ct);
                changes.Mark(id);return true;
            },ct);
    }

    public async Task<HospitalDto> GetHospitalAsync(string slug, Guid accountId, CancellationToken ct)
    {
        var villageId = await FindVillageIdAsync(slug, ct);
        return await WithVillageAsync(villageId, async db =>
        {
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, ct);
            if (village.OwnerAccountId != accountId && !await db.Citizens.AnyAsync(c => c.VillageId == villageId && c.AccountId == accountId, ct))
                throw new UnauthorizedAccessException();
            var reply = await SyncHospitalAsync(db, villageId, ct, true, accountId);
            var citizens = await db.Citizens.Where(c => c.VillageId == villageId && c.Status == CitizenStatus.Active).ToListAsync(ct);
            var me = citizens.SingleOrDefault(c => c.AccountId == accountId);
            var uid = await TelegramIdAsync(db, accountId, ct);
            var state = reply?.Patients.FirstOrDefault(p => p.Id == uid?.ToString());
            return new HospitalDto(reply is not null, HospitalRules.Level(village), reply?.Balance, me?.Recovery,
                state?.Tiers ?? reply?.Patients.FirstOrDefault()?.Tiers ?? [], citizens.Where(c => HospitalRules.IsAdmitted(c, time.GetUtcNow()) || c.InjuredUntil>time.GetUtcNow())
                    .Select(c => new HospitalPatientDto(c.Id, c.Name, HospitalRules.IsAdmitted(c,time.GetUtcNow()) ? c.Recovery : new ExternalRecovery{Cause="battle",Until=c.InjuredUntil,Available=true},c.Appearance)).ToList());
        }, ct);
    }

    public async Task<HospitalTreatmentReply> TreatAsync(string slug, Guid accountId, HospitalHealRequest request, CancellationToken ct)
    {
        if (!hospitalBridge.Enabled) throw new GameRuleException("hospital.unavailable", "Hospital connection is not configured.");
        if (!Guid.TryParse(request.RequestId, out _) || request.ExpectedPrice is < 0 or > 1000000
            || string.IsNullOrEmpty(request.EpisodeId) || request.EpisodeId.Length > 80 || string.IsNullOrEmpty(request.Code) || request.Code.Length > 32)
            throw new GameRuleException("hospital.invalidRequest", "Refresh the treatment menu.");
        var villageId = await FindVillageIdAsync(slug, ct);
        return await WithVillageAsync(villageId, async db =>
        {
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, ct);
            if (village.OwnerAccountId != accountId && !await db.Citizens.AnyAsync(c => c.VillageId == villageId && c.AccountId == accountId, ct))
                throw new UnauthorizedAccessException();
            var payer = await TelegramIdAsync(db, accountId, ct) ?? throw new UnauthorizedAccessException();
            var patient = await db.Citizens.SingleOrDefaultAsync(c => c.Id == request.PatientId && c.VillageId == villageId, ct)
                ?? throw new UnauthorizedAccessException();
            var patientId = await TelegramIdAsync(db, patient.AccountId, ct) ?? throw new UnauthorizedAccessException();
            try
            {
                var result = await hospitalBridge.HealAsync(payer, patientId, request, ct);
                await SyncHospitalAsync(db, villageId, ct, true, accountId);
                return result;
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
            {
                if (ct.IsCancellationRequested) throw;
                // Browser must retain this operation ID and retry it, never generate a replacement after timeout.
                return new HospitalTreatmentReply("pending", "connection_lost", null, null, null);
            }
        }, ct);
    }

    public Task<VillageStateDto> UpgradeHospitalAsync(string slug, Guid accountId, string plotId, CancellationToken ct) =>
        RuleAsync(slug, accountId, "hospitalUpgrade", plotId,
            (village, now) => ConstructionRules.UpgradeHospital(village, plotId, now), ct);
}
