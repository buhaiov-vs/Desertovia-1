using Desertod.Domain;
using Desertod.Domain.Combat;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

/// <param name="EveryMonster">Two of each enemy kind, dungeon dwellers included; otherwise a dozen zombies and skeletons.</param>
public sealed record DevRaidRequest(bool EveryMonster);

/// <summary>Development and local deployments only; the endpoints refuse unless <see cref="GameOptions.DevTools"/> is on.</summary>
public sealed partial class GameService
{
    private static readonly TimeSpan DevRaidWarning = TimeSpan.FromSeconds(5);

    /// <summary>Starts one wave that strikes a few seconds later, for any player and without the owner's raid editor.</summary>
    public async Task<VillageStateDto> DevLaunchRaidAsync(string slug, Guid actor, DevRaidRequest request, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct);
            if (village.Raids.Any(r => r.IsPending) || await db.RaidBattles.AnyAsync(b => b.VillageId == village.Id && b.FinishedAt == null, ct))
                throw new GameRuleException("raid.pending", "Finish or cancel the existing raid first.");
            var kinds = EnemyCatalog.Enemies.Select(e => e.Kind).ToArray();
            var settings = new RaidSettings
            {
                Mode = "manual",
                Count = request.EveryMonster ? kinds.Length * 2 : 12,
                WeakPercent = request.EveryMonster ? 0 : 35,
                ElitePercent = request.EveryMonster ? 0 : 20,
                Species = request.EveryMonster ? [] : [new() { Enemy = EnemyKind.Zombie, Percent = 50 }, new() { Enemy = EnemyKind.Skeleton, Percent = 50 }],
            };
            var now = time.GetUtcNow();
            var raid = new Raid
            {
                Source = RaidSource.Undead, Enemy = settings.Enemy, Wave = 1, Waves = 1,
                AnnouncedAt = now, StrikesAt = now + DevRaidWarning,
            };
            var battle = await BuildConfiguredRaidAsync(db, village, raid, settings, now, ct);
            if (request.EveryMonster)
            {
                // The editor refuses dungeon dwellers, so the zombies it planned are rebuilt one kind after another.
                for (var index = 0; index < battle.Units.Count; index++)
                {
                    var unit = battle.Units[index];
                    var definition = EnemyCatalog.Get(kinds[index % kinds.Length]);
                    var factor = RaidBalance.Factor(unit.Variant);
                    unit.Enemy = definition.Kind;
                    unit.Health = unit.MaximumHealth = (12 + definition.Power * 2) * factor;
                    unit.Attack = (1.5 + definition.Power * .4) * factor;
                    unit.Speed = definition.Kind == EnemyKind.Zombie ? .55 : .75;
                }
            }
            battle.EventId = Guid.CreateVersion7();
            battle.InitiatedBy = actor;
            battle.Event("announced", "developer", now);
            village.Raids.Add(raid);
            db.RaidBattles.Add(battle);
            RaidRules.UpdateNextRaidAt(village);
            db.AuditActor = actor;
            db.AuditOperation = "dev-raid-launch";
            await db.SaveChangesAsync(ct);
            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, ct);
        }, ct);

    /// <summary>
    /// Tonight's raid right now: every den that may send at night sends its pack, striking a few seconds later.
    /// Skips the hour, the first peaceful day and the activity check. A village no den may send to yet, as every new
    /// one, gets the lowest-level dens whose foe may raid instead, so the button always brings an attack.
    /// </summary>
    public async Task<VillageStateDto> DevNightRaidAsync(string slug, Guid actor, CancellationToken ct) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, ct), async db =>
        {
            var village = await LoadVillageAsync(db, slug, ct);
            if (village.Raids.Any(r => r.IsPending) || await db.RaidBattles.AnyAsync(b => b.VillageId == village.Id && b.FinishedAt == null, ct))
                throw new GameRuleException("raid.pending", "Finish or cancel the existing raid first.");
            var now = time.GetUtcNow();
            var senders = RaidRules.NightSenders(village);
            var raid = RaidRules.SendNightRaids(village, senders.Count > 0 ? senders : RaidRules.NearestRaidingDens(village), now + DevRaidWarning, DevRaidWarning)
                ?? throw new GameRuleException("raid.noDens", "No den may send beasts to this village yet.");
            await PrepareBattleAsync(db, village, raid, now, ct);
            db.AuditActor = actor;
            db.AuditOperation = "dev-night-raid";
            await db.SaveChangesAsync(ct);
            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, ct);
        }, ct);

    /// <summary>Cancels every pending raid, beasts included, for any player.</summary>
    public Task<VillageStateDto> DevCancelRaidsAsync(string slug, Guid actor, CancellationToken ct) =>
        CancelRaidsAsync(slug, actor, developer: true, ct);
}
