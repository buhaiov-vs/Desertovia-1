using Desertod.Domain;
using Desertod.Domain.Combat;
using Desertod.Domain.World;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

/// <summary>A present citizen standing far enough out to be near a den.</summary>
public sealed record DenWaker(Guid VillageId, string Slug, Guid CitizenId, Guid AccountId, TilePoint Position);

/// <summary>The citizen stopped at a den and comes into its fight.</summary>
public sealed record DenEngagement(DenFighter Fighter, TilePoint Position, TilePoint? Destination);

/// <summary>Den fights run in memory (<see cref="DenRuntime"/>); the citizen is written when they come in and when they come out.</summary>
public sealed partial class GameService
{
    /// <summary>Closer to the camp than this, in tiles, no den can be in reach.</summary>
    private static readonly double DenFreeRadius = SpawnRings.Levels[0].Floor - DenGuards.AggroRange - 2;

    public async Task<IReadOnlyList<DenWaker>> FindDenWakersAsync(IReadOnlyCollection<Guid> citizenIds, CancellationToken cancellationToken)
    {
        if (citizenIds.Count == 0)
        {
            return [];
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var camp = WorldMap.Places[WorldMap.Camp];
        var candidates = (await db.Citizens.AsNoTracking()
                .Where(c => citizenIds.Contains(c.Id) && c.CurrentTaskId == null && c.DungeonRunId == null && c.RescuePatientId == null)
                .ToListAsync(cancellationToken))
            .Where(c => DenRules.CanEngage(c, now))
            .Select(c => (Citizen: c, Position: DenRules.Position(c, now)))
            .Where(c => c.Position.DistanceTo(camp) >= DenFreeRadius)
            .ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        var villageIds = candidates.Select(c => c.Citizen.VillageId).Distinct().ToList();
        var slugs = await db.Villages.AsNoTracking()
            .Where(v => villageIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Slug, cancellationToken);
        return candidates
            .Where(c => slugs.ContainsKey(c.Citizen.VillageId))
            .Select(c => new DenWaker(c.Citizen.VillageId, slugs[c.Citizen.VillageId], c.Citizen.Id, c.Citizen.AccountId, c.Position))
            .ToList();
    }

    /// <summary>Fighting citizens who took up a task, went to a dungeon or are no longer fit: they leave their den fight.</summary>
    public async Task<IReadOnlyList<Guid>> FindDenDropoutsAsync(IReadOnlyCollection<Guid> citizenIds, CancellationToken cancellationToken)
    {
        if (citizenIds.Count == 0)
        {
            return [];
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var citizens = await db.Citizens.AsNoTracking().Where(c => citizenIds.Contains(c.Id)).ToListAsync(cancellationToken);
        return citizenIds
            .Where(id => citizens.FirstOrDefault(c => c.Id == id) is not { } citizen || !DenRules.CanEngage(citizen, now))
            .ToList();
    }

    /// <summary>A den woke for the citizen: they stop where they are. Null when they may no longer be fought.</summary>
    public Task<DenEngagement?> EngageDenAsync(DenWaker waker, string denId, CancellationToken cancellationToken) =>
        WithVillageAsync(waker.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleOrDefaultAsync(c => c.Id == waker.CitizenId && c.VillageId == waker.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            if (citizen is null || !DenRules.CanEngage(citizen, now) || HasHospitalRestriction(citizen.AccountId))
            {
                return null;
            }

            var village = await db.Villages.SingleAsync(v => v.Id == waker.VillageId, cancellationToken);
            var destination = DenRules.Halt(citizen, village, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, village.Slug, "denEngaged", denId);

            await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return new DenEngagement(DenRules.Fighter(citizen), DenRules.Position(citizen, now), destination);
        }, cancellationToken);

    public Task SettleDenAsync(Guid villageId, string denId, DenOutcome outcome, CancellationToken cancellationToken) =>
        WithVillageAsync(villageId, async db =>
        {
            var citizen = await db.Citizens.SingleOrDefaultAsync(c => c.Id == outcome.CitizenId && c.VillageId == villageId, cancellationToken);
            if (citizen is null)
            {
                return false;
            }

            var village = await db.Villages.SingleAsync(v => v.Id == villageId, cancellationToken);
            var now = time.GetUtcNow();
            DenRules.Settle(citizen, village, outcome, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, village.Slug, "denSettled",
                $"{denId} kills={outcome.Kills} xp={outcome.Experience} knockedOut={outcome.KnockedOut}");

            await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return true;
        }, cancellationToken);

    /// <summary>The player picks a protector of the den their citizen fights at.</summary>
    public async Task AttackDenAsync(string slug, Guid accountId, string denId, DenAttackRequest request, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        if (HasHospitalRestriction(accountId))
        {
            throw new GameRuleException("hospital.unavailable", "Wait until recovery.");
        }

        var live = dens.Find(key.VillageId, denId);
        var picked = false;
        if (live is not null)
        {
            lock (live.Gate)
            {
                picked = live.Fight.Attack(key.Id, request.Protector);
            }
        }

        if (!picked)
        {
            throw new GameRuleException("den.notFighting", "You are not fighting at this den.");
        }
    }

    /// <summary>"Come here" while a den fight lasts: the fight moves the citizen, the database learns where they ended up when it is over.</summary>
    private async Task<CitizenDto?> GoToInDenFightAsync(Guid citizenId, TilePoint spot, CancellationToken cancellationToken)
    {
        if (dens.Fighting(citizenId) is not { } live)
        {
            return null;
        }

        lock (live.Gate)
        {
            if (!live.Fight.MoveTo(citizenId, spot))
            {
                throw new GameRuleException("spot.unreachable", "You cannot walk there.");
            }
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var citizen = await db.Citizens.AsNoTracking().SingleAsync(c => c.Id == citizenId, cancellationToken);
        var village = await db.Villages.AsNoTracking().SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
        return citizen.ToDto(village, time.GetUtcNow());
    }
}
