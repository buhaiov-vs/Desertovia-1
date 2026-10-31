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
    public async Task<IReadOnlyList<DenWaker>> FindDenWakersAsync(IReadOnlyCollection<Guid> citizenIds, CancellationToken cancellationToken)
    {
        if (citizenIds.Count == 0)
        {
            return [];
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var candidates = (await db.Citizens.AsNoTracking()
                .Where(c => citizenIds.Contains(c.Id) && c.DungeonRunId == null && c.RescuePatientId == null)
                .ToListAsync(cancellationToken))
            .Where(c => DenRules.CanEngage(c, now))
            .Select(c => (Citizen: c, Position: DenRules.Position(c, now)))
            // Foes keep clear of the vale, so no one inside it can be in reach.
            .Where(c => Haunts.FromVale(c.Position) > 0)
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

    /// <summary>
    /// Left click on a protector or a roaming foe. In a fight already, that foe is the one struck.
    /// Standing still at its den, the citizen steps in and strikes it. Farther off, the citizen walks there
    /// and strikes when the fight opens. A plain walk cancels this, and deals no damage.
    /// </summary>
    public async Task AttackDenAsync(string slug, Guid accountId, string denId, DenAttackRequest request, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        if (HasHospitalRestriction(accountId))
        {
            throw new GameRuleException("hospital.unavailable", "Wait until recovery.");
        }

        var now = time.GetUtcNow();
        var point = dens.Locate(key.VillageId, denId)
            ?? throw new GameRuleException("den.unknown", "There is no such foe.");
        var count = point.Roams ? 1 : point.Protectors;
        if (request.Protector < 0 || request.Protector >= count)
        {
            throw new GameRuleException("den.unknown", "There is no such foe.");
        }

        if (dens.Find(key.VillageId, denId) is { } live)
        {
            bool mine, struck;
            lock (live.Gate)
            {
                mine = live.Fight.Fights(key.Id);
                struck = mine && live.Fight.Attack(key.Id, request.Protector);
            }

            if (mine)
            {
                dens.ClearAttack(key.Id);
                if (!struck)
                {
                    throw new GameRuleException("den.down", "That foe is already down.");
                }

                return;
            }

            lock (live.Gate)
            {
                if (live.Fight.Hero is not null)
                {
                    throw new GameRuleException("den.busy", "Someone is already fighting there.");
                }
            }
        }

        var spot = dens.Stand(key.VillageId, point, request.Protector, now);
        if (dens.Fighting(key.Id) is { } elsewhere)
        {
            bool moved;
            lock (elsewhere.Gate)
            {
                moved = elsewhere.Fight.MoveTo(key.Id, spot);
            }

            if (!moved)
            {
                throw new GameRuleException("spot.unreachable", "You cannot walk there.");
            }

            dens.OrderAttack(key.Id, point.Id, request.Protector);
            return;
        }

        TilePoint position;
        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var citizen = await db.Citizens.AsNoTracking().SingleAsync(c => c.Id == key.Id, cancellationToken);
            if (!DenRules.CanEngage(citizen, now))
            {
                throw new GameRuleException("den.unavailable", "You cannot fight right now.");
            }

            position = DenRules.Position(citizen, now);
        }

        var anchor = point.Roams ? spot : point.Center;
        if (InReach(point, position, anchor, now))
        {
            if (!await EngageAttackAsync(key.VillageId, slug, accountId, key.Id, point, request.Protector, cancellationToken))
            {
                dens.OrderAttack(key.Id, point.Id, request.Protector);
            }

            return;
        }

        await GoToAsync(slug, accountId, new GoToRequest(spot), cancellationToken);
        dens.OrderAttack(key.Id, point.Id, request.Protector);
    }

    /// <summary>The citizen is already in reach: open the fight and strike. False when another open is in progress.</summary>
    private async Task<bool> EngageAttackAsync(
        Guid villageId, string slug, Guid accountId, Guid citizenId, SpawnPoint point, int protector, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var citizen = await db.Citizens.AsNoTracking().SingleOrDefaultAsync(c => c.Id == citizenId && c.VillageId == villageId, cancellationToken);
        if (citizen is null || !DenRules.CanEngage(citizen, now))
        {
            return false;
        }

        var position = DenRules.Position(citizen, now);
        var anchor = point.Roams ? dens.Stand(villageId, point, 0, now) : point.Center;
        if (!InReach(point, position, anchor, now))
        {
            return false;
        }

        var live = dens.Open(villageId, slug, point, now);
        lock (live.Gate)
        {
            if (live.Opening || live.Fight.Hero is not null || !live.Fight.Wakes(position))
            {
                return false;
            }

            live.Opening = true;
        }

        try
        {
            if (await EngageDenAsync(new DenWaker(villageId, slug, citizenId, accountId, position), point.Id, cancellationToken) is not { } engagement)
            {
                return false;
            }

            lock (live.Gate)
            {
                if (!live.Fight.Engage(engagement.Fighter, engagement.Position, engagement.Destination))
                {
                    return false;
                }

                live.HeroAccountId = accountId;
                dens.ClearAttack(citizenId);
                return live.Fight.Attack(citizenId, protector);
            }
        }
        finally
        {
            lock (live.Gate)
            {
                live.Opening = false;
            }
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

    /// <summary>Close enough that the den, or the roaming foe, notices the citizen.</summary>
    private static bool InReach(SpawnPoint point, TilePoint position, TilePoint anchor, DateTimeOffset now) =>
        point.Roams
            ? position.DistanceTo(anchor) <= DenGuards.AggroRange
            : DenGuards.Alerts(point, position, now);
}
