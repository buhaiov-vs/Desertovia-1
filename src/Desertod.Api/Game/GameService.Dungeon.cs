using Desertod.Domain;
using Desertod.Domain.Catalog;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

/// <summary>A run whose march is over: the worker starts its fight in memory.</summary>
public sealed record DungeonStart(
    Guid RunId,
    Guid VillageId,
    string Slug,
    DungeonDefinition Dungeon,
    int Seed,
    IReadOnlyList<DungeonFighter> Fighters,
    IReadOnlyList<DungeonRunHeroDto> Heroes,
    IReadOnlyDictionary<Guid, Guid> Owners);

/// <summary>A hero out of the fight, with what they did in it.</summary>
public sealed record DungeonExit(Guid HeroId, DungeonHeroOutcome Outcome);

/// <summary>Dungeon runs: written to the database when a hero joins or leaves and when the run changes phase.</summary>
public sealed partial class GameService
{
    public async Task<VillageStateDto> OpenDungeonAsync(string slug, Guid accountId, string dungeonId, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            if (await db.DungeonRuns.AnyAsync(r => r.VillageId == village.Id && r.Phase != DungeonPhase.Ended, cancellationToken))
            {
                throw new GameRuleException("dungeon.alreadyOpen", "The tribe is already off to a dungeon.");
            }

            var now = time.GetUtcNow();
            var run = DungeonRules.Open(village, dungeonId, await TsarNameAsync(db, village, cancellationToken), Random.Shared.Next(), now);
            db.DungeonRuns.Add(run);
            await db.SaveChangesAsync(cancellationToken);
            logger.DungeonPhase(slug, run.Id, run.Phase.ToString(), run.Heroes.Count);

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);

    public async Task<VillageStateDto> CancelDungeonAsync(string slug, Guid accountId, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            var run = await RequireGatheringAsync(db, village.Id, cancellationToken);
            var citizens = await db.Citizens.Where(c => c.DungeonRunId == run.Id).ToListAsync(cancellationToken);
            var now = time.GetUtcNow();
            DungeonRules.Cancel(run, village, citizens, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.DungeonEnded(slug, run.Id, run.Outcome.ToString(), 0, run.Foes);

            foreach (var citizen in citizens)
            {
                await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            }

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);

    /// <summary>The classes the citizen may pick now, with the health and damage each would give.</summary>
    public async Task<IReadOnlyList<HeroClassOfferDto>> GetHeroClassesAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var citizen = await db.Citizens.AsNoTracking().SingleAsync(c => c.Id == key.Id, cancellationToken);
        var village = await db.Villages.AsNoTracking().SingleAsync(v => v.Id == key.VillageId, cancellationToken);
        var run = await db.DungeonRuns.AsNoTracking()
            .Where(r => r.VillageId == village.Id && r.Phase == DungeonPhase.Gathering)
            .FirstOrDefaultAsync(cancellationToken);
        var hero = run?.Hero(citizen.Id);
        var leather = village.Stock(ResourceKind.LeatherArmor) + citizen.Carried(ResourceKind.LeatherArmor) > 0
                      || citizen.Armor == ResourceKind.LeatherArmor;
        return DungeonRules.AvailableClasses(citizen, village, hero)
            .Select(HeroClassCatalog.Get)
            .Select(definition => new HeroClassOfferDto(
                definition.Class,
                Math.Round(DungeonRules.Health(citizen, definition), 1),
                Math.Round(DungeonRules.Damage(citizen, definition), 1),
                definition.Range,
                definition.Weapon,
                definition.Armor ?? (leather ? ResourceKind.LeatherArmor : null)))
            .ToList();
    }

    /// <summary>The citizen answers the Tsar's call, or switches class while the gathering lasts.</summary>
    public async Task<CitizenDto> JoinDungeonAsync(string slug, Guid accountId, HeroClass heroClass, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var run = await RequireGatheringAsync(db, village.Id, cancellationToken);
            var now = time.GetUtcNow();
            DungeonRules.Join(run, village, citizen, heroClass, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "joinDungeon", heroClass.ToString());

            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    public async Task<CitizenDto> LeaveDungeonAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var run = await RequireGatheringAsync(db, village.Id, cancellationToken);
            var now = time.GetUtcNow();
            DungeonRules.Leave(run, village, citizen, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "leaveDungeon", "");

            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>
    /// Closes the gatherings that are over (the heroes set out) and ends the marches that are over. Returns the runs
    /// whose fight starts now.
    /// </summary>
    public async Task<IReadOnlyList<DungeonStart>> AdvanceDungeonsAsync(CancellationToken cancellationToken)
    {
        List<(Guid Id, Guid VillageId)> due;
        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var now = time.GetUtcNow();
            due = (await db.DungeonRuns
                    .Where(r => (r.Phase == DungeonPhase.Gathering && r.GatheringEndsAt <= now)
                                || (r.Phase == DungeonPhase.Marching && r.MarchEndsAt <= now))
                    .Select(r => new { r.Id, r.VillageId })
                    .ToListAsync(cancellationToken))
                .Select(r => (r.Id, r.VillageId))
                .ToList();
        }

        var started = new List<DungeonStart>();
        foreach (var (runId, villageId) in due)
        {
            if (await AdvanceDungeonAsync(runId, villageId, cancellationToken) is { } start)
            {
                started.Add(start);
            }
        }

        return started;
    }

    private Task<DungeonStart?> AdvanceDungeonAsync(Guid runId, Guid villageId, CancellationToken cancellationToken) =>
        WithVillageAsync(villageId, async db =>
        {
            var run = await db.DungeonRuns.SingleAsync(r => r.Id == runId, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, cancellationToken);
            var citizens = await db.Citizens.Where(c => c.DungeonRunId == run.Id).ToListAsync(cancellationToken);
            var now = time.GetUtcNow();
            DungeonStart? start = null;
            if (run.Phase == DungeonPhase.Gathering && run.GatheringEndsAt <= now)
            {
                village.EnsureCamp(now);
                DungeonRules.March(run, village, citizens, now);
            }
            else if (run.Phase == DungeonPhase.Marching && run.MarchEndsAt <= now)
            {
                DungeonRules.Start(run, now);
                start = await BuildStartAsync(db, run, village, citizens, cancellationToken);
            }
            else
            {
                return null;
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.DungeonPhase(village.Slug, run.Id, run.Phase.ToString(), run.Heroes.Count);
            foreach (var citizen in citizens)
            {
                await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
            }

            changes.Mark(village.Id);
            return start;
        }, cancellationToken);

    /// <summary>
    /// Heroes are out of the fight: their goods are stored, wounds and broken gear applied, and they walk home.
    /// With <paramref name="outcome"/> the run ends too, and everyone still in it comes out.
    /// </summary>
    public Task SettleDungeonAsync(
        Guid runId,
        Guid villageId,
        IReadOnlyList<DungeonExit> exits,
        DungeonOutcome? outcome,
        int slain,
        CancellationToken cancellationToken) =>
        WithVillageAsync(villageId, async db =>
        {
            var run = await db.DungeonRuns.SingleAsync(r => r.Id == runId, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, cancellationToken);
            var citizens = await db.Citizens.Where(c => c.DungeonRunId == run.Id).ToListAsync(cancellationToken);
            var now = time.GetUtcNow();
            foreach (var exit in exits)
            {
                var hero = run.Heroes.FirstOrDefault(h => h.CitizenId == exit.HeroId || (h.IsTsar && h.AccountId == exit.HeroId));
                if (hero is null)
                {
                    continue;
                }

                var citizen = citizens.FirstOrDefault(c => c.Id == hero.CitizenId);
                DungeonRules.Settle(run, hero, citizen, village, exit.Outcome, Random.Shared, now);
            }

            run.Slain = slain;
            if (outcome is { } ended)
            {
                DungeonRules.Finish(run, ended, slain, now);
            }

            await db.SaveChangesAsync(cancellationToken);
            foreach (var citizen in citizens)
            {
                await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
            }

            if (outcome is not null)
            {
                logger.DungeonEnded(village.Slug, run.Id, run.Outcome.ToString(), run.Slain, run.Foes);
                await notifier.DungeonEnded(village.Slug, run.ToDto());
            }

            changes.Mark(village.Id);
            return true;
        }, cancellationToken);

    /// <summary>After a restart nothing of a fight is left in memory: unfinished runs end as abandoned and everyone goes home.</summary>
    public async Task AbandonDungeonsAsync(CancellationToken cancellationToken)
    {
        List<(Guid Id, Guid VillageId)> unfinished;
        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            unfinished = (await db.DungeonRuns
                    .Where(r => r.Phase != DungeonPhase.Ended)
                    .Select(r => new { r.Id, r.VillageId })
                    .ToListAsync(cancellationToken))
                .Select(r => (r.Id, r.VillageId))
                .ToList();
        }

        foreach (var (runId, villageId) in unfinished)
        {
            await WithVillageAsync(villageId, async db =>
            {
                var run = await db.DungeonRuns.SingleAsync(r => r.Id == runId, cancellationToken);
                var village = await db.Villages.SingleAsync(v => v.Id == villageId, cancellationToken);
                var citizens = await db.Citizens.Where(c => c.DungeonRunId == run.Id).ToListAsync(cancellationToken);
                DungeonRules.Abandon(run, village, citizens, time.GetUtcNow());
                await db.SaveChangesAsync(cancellationToken);
                logger.DungeonEnded(village.Slug, run.Id, run.Outcome.ToString(), run.Slain, run.Foes);
                changes.Mark(village.Id);
                return true;
            }, cancellationToken);
        }
    }

    /// <summary>A run as stored, for viewers of a run that is not fought in memory: not started yet, or over.</summary>
    public async Task<DungeonRunDto?> GetDungeonRunAsync(Guid runId, Guid? accountId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.DungeonRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var village = await db.Villages.AsNoTracking().SingleAsync(v => v.Id == run.VillageId, cancellationToken);
        var citizenIds = run.Heroes.Where(h => h.CitizenId is not null).Select(h => h.CitizenId!.Value).ToList();
        var citizens = await db.Citizens.AsNoTracking().Where(c => citizenIds.Contains(c.Id)).ToListAsync(cancellationToken);
        var heroes = await RunHeroesAsync(db, run, village, citizens, cancellationToken);
        var dungeon = DungeonCatalog.Get(run.DungeonId);
        var mine = accountId is { } id ? run.Heroes.FirstOrDefault(h => h.AccountId == id) : null;
        return new DungeonRunDto(
            run.Id,
            village.Slug,
            dungeon.Id,
            dungeon.Width,
            dungeon.Height,
            DungeonMapper.EncodeTiles(dungeon),
            dungeon.Entrance,
            (int)dungeon.TimeLimit.TotalSeconds,
            heroes,
            mine is null ? null : mine.CitizenId ?? mine.AccountId,
            run.ToDto(),
            null);
    }

    private async Task<DungeonStart> BuildStartAsync(
        DesertodDbContext db,
        DungeonRun run,
        Village village,
        IReadOnlyList<Citizen> citizens,
        CancellationToken cancellationToken)
    {
        var fighters = new List<DungeonFighter>();
        var owners = new Dictionary<Guid, Guid>();
        foreach (var hero in run.Heroes)
        {
            var definition = HeroClassCatalog.Get(hero.Class);
            if (hero.IsTsar)
            {
                fighters.Add(new DungeonFighter(hero.AccountId, hero.AccountId, hero.Name, hero.Class,
                    HeroClassCatalog.VoivodeHealth, HeroClassCatalog.VoivodeDamage, 0, null, false));
                owners[hero.AccountId] = hero.AccountId;
                continue;
            }

            if (citizens.FirstOrDefault(c => c.Id == hero.CitizenId) is not { } citizen)
            {
                continue;
            }

            fighters.Add(new DungeonFighter(
                citizen.Id,
                citizen.AccountId,
                citizen.Name,
                hero.Class,
                DungeonRules.Health(citizen, definition),
                DungeonRules.Damage(citizen, definition),
                DungeonRules.Protection(citizen),
                hero.Class == HeroClass.Archer ? citizen.Arrows : null,
                definition.Heal > 0 && hero.Remedies > 0));
            owners[citizen.Id] = citizen.AccountId;
        }

        var heroes = await RunHeroesAsync(db, run, village, citizens, cancellationToken);
        return new DungeonStart(run.Id, village.Id, village.Slug, DungeonCatalog.Get(run.DungeonId), run.Seed, fighters, heroes, owners);
    }

    private static async Task<IReadOnlyList<DungeonRunHeroDto>> RunHeroesAsync(
        DesertodDbContext db,
        DungeonRun run,
        Village village,
        IReadOnlyList<Citizen> citizens,
        CancellationToken cancellationToken)
    {
        var owner = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == village.OwnerAccountId)
            .Select(a => new { a.Character })
            .SingleAsync(cancellationToken);
        var heroes = new List<DungeonRunHeroDto>();
        foreach (var hero in run.Heroes)
        {
            var definition = HeroClassCatalog.Get(hero.Class);
            if (hero.IsTsar)
            {
                heroes.Add(new DungeonRunHeroDto(hero.AccountId, null, hero.Name, hero.Class, owner.Character?.Appearance, HeroClassCatalog.VoivodeHealth));
            }
            else if (citizens.FirstOrDefault(c => c.Id == hero.CitizenId) is { } citizen)
            {
                heroes.Add(new DungeonRunHeroDto(citizen.Id, citizen.Id, citizen.Name, hero.Class, citizen.Appearance,
                    Math.Round(DungeonRules.Health(citizen, definition), 1)));
            }
            else
            {
                heroes.Add(new DungeonRunHeroDto(hero.CitizenId!.Value, hero.CitizenId, hero.Name, hero.Class, null, 0));
            }
        }

        return heroes;
    }

    private static async Task<string> TsarNameAsync(DesertodDbContext db, Village village, CancellationToken cancellationToken)
    {
        var owner = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == village.OwnerAccountId)
            .Select(a => new { a.DisplayName, a.Character })
            .SingleAsync(cancellationToken);
        return owner.Character?.Name ?? owner.DisplayName;
    }

    private static async Task<DungeonRun> RequireGatheringAsync(DesertodDbContext db, Guid villageId, CancellationToken cancellationToken) =>
        await db.DungeonRuns
            .Where(r => r.VillageId == villageId && r.Phase == DungeonPhase.Gathering)
            .OrderByDescending(r => r.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new GameRuleException("dungeon.notGathering", "Nobody is gathering for a dungeon now.");
}
