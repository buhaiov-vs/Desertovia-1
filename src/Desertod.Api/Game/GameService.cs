using System.Text;
using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.Weather;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Desertod.Api.Game;

public sealed class NotFoundException(string code, string message, IReadOnlyDictionary<string, object>? arguments = null)
    : Exception(message)
{
    public string Code { get; } = code;

    public IReadOnlyDictionary<string, object> Arguments { get; } = arguments ?? new Dictionary<string, object>();
}

/// <summary>A citizen whose cycle has ended but is not banked yet.</summary>
public sealed record DueCitizen(Guid VillageId, Guid CitizenId);

public sealed partial class GameService(
    IDbContextFactory<DesertodDbContext> contextFactory,
    TimeProvider time,
    VillageNotifier notifier,
    VillageStateChanges changes,
    CitizenPresence presence,
    ILogger<GameService> logger)
{
    private const int MaximumAttempts = 5;

    private static readonly TimeSpan SlowLockThreshold = TimeSpan.FromMilliseconds(100);
    private const string SuperviseTaskId = "supervise";
    private const string BuildTaskId = "build";

    /// <summary>Citizens settled under one hold of the village lock; each takes several queries.</summary>
    private const int SettlementChunkSize = 10;

    /// <summary>Taken by <c>GET /api/villages/public</c>, so no village may use it.</summary>
    private const string PublicListSlug = "public";

    private readonly VillageLocks locks = new();

    /// <param name="IsDue">The cycle has ended and is not banked yet.</param>
    private sealed record CitizenKey(Guid Id, Guid VillageId, bool IsDue);

    /// <param name="CompletedInOrder">Finished project ids, oldest first.</param>
    private sealed record VillageContext(WorkContext Work, IReadOnlyList<string> CompletedInOrder, IReadOnlyList<Guid> ActiveCitizenIds);

    public async Task<IReadOnlyList<VillageListItemDto>> ListVillagesAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var citizenVillageIds = db.Citizens.Where(c => c.AccountId == accountId).Select(c => c.VillageId);

        return await db.Villages
            .Where(v => v.OwnerAccountId == accountId || citizenVillageIds.Contains(v.Id))
            .OrderBy(v => v.Name)
            .Select(v => new VillageListItemDto(v.Slug, v.Name, v.OwnerAccountId == accountId))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Public villages for the home page, the busiest first.</summary>
    public async Task<IReadOnlyList<PublicVillageDto>> ListPublicVillagesAsync(CancellationToken cancellationToken)
    {
        const int limit = 50;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Villages
            .Where(v => !v.IsPrivate)
            .Select(v => new
            {
                Village = v,
                Citizens = db.Citizens.Count(c => c.VillageId == v.Id && c.Status == CitizenStatus.Active),
            })
            .OrderByDescending(x => x.Citizens)
            .ThenBy(x => x.Village.Name)
            .Take(limit)
            .Select(x => new PublicVillageDto(x.Village.Slug, x.Village.Name, x.Village.Description, x.Citizens))
            .ToListAsync(cancellationToken);
    }

    public async Task<VillageStateDto> CreateVillageAsync(Guid accountId, CreateVillageRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var village = Village.Create("pending", request.Name ?? "", request.Description, request.IsPrivate, accountId, now);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var baseSlug = Slugify(village.Name);
        var slug = baseSlug;
        for (var suffix = 2; slug == PublicListSlug || await db.Villages.AnyAsync(v => v.Slug == slug, cancellationToken); suffix++)
        {
            slug = $"{baseSlug}-{suffix}";
        }

        village.Slug = slug;
        db.Villages.Add(village);
        await db.SaveChangesAsync(cancellationToken);
        logger.VillageCreated(accountId, slug);

        return await BuildStateAsync(db, village, now, cancellationToken);
    }

    public async Task<VillageDto> GetVillageAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var village = await LoadVillageAsync(db, slug, cancellationToken);
        if (village.EnsureCamp(time.GetUtcNow()))
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var citizens = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow();
        var isOwner = village.OwnerAccountId == accountId;
        return new VillageDto(
            await BuildStateAsync(db, village, now, cancellationToken),
            isOwner,
            isOwner ? village.InviteCode : null,
            citizens.Select(c => c.ToSummaryDto(now)).ToList(),
            citizens.Where(c => !presence.IsPresent(c.Id)).Select(c => c.Id).ToList());
    }

    /// <summary>The player's village page connected to the hub; a citizen of this village is present while it stays open.</summary>
    public async Task ConnectAsync(string slug, Guid accountId, string connectionId, CancellationToken cancellationToken)
    {
        if (await FindCitizenAsync(slug, accountId, cancellationToken) is { } key && presence.Connect(key.Id, slug, connectionId))
        {
            await notifier.CitizenBack(slug, key.Id);
        }
    }

    public void Disconnect(string connectionId) => presence.Disconnect(connectionId);

    /// <summary>The citizen did something, so they are present.</summary>
    private async Task SeeAsync(string slug, Guid citizenId)
    {
        if (presence.Touch(citizenId, slug))
        {
            await notifier.CitizenBack(slug, citizenId);
        }
    }

    public async Task<InviteDto> NewInviteCodeAsync(string slug, Guid accountId, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            if (!village.IsPrivate)
            {
                throw new GameRuleException("village.notPrivate", "Anyone can join this village; it needs no invite code.");
            }

            var code = village.NewInviteCode();
            await db.SaveChangesAsync(cancellationToken);
            logger.TsarActed(slug, "newInviteCode", "");
            return new InviteDto(code);
        }, cancellationToken);

    public async Task<CitizenDto?> GetCitizenAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await FindCitizenAsync(slug, accountId, cancellationToken);
        if (key is null)
        {
            return null;
        }

        await SeeAsync(slug, key.Id);
        if (key.IsDue)
        {
            await SettleVillageAsync(key.VillageId, [key.Id], cancellationToken);
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
        var village = await db.Villages.AsNoTracking().SingleAsync(v => v.Id == key.VillageId, cancellationToken);
        return citizen.ToDto(village, time.GetUtcNow());
    }

    public async Task<CitizenDto> JoinVillageAsync(
        string slug,
        Guid accountId,
        JoinVillageRequest request,
        CancellationToken cancellationToken)
    {
        var villageId = await FindVillageIdAsync(slug, cancellationToken);
        CitizenDto created;
        using (await AcquireLockAsync(villageId, cancellationToken))
        {
            created = await AddCitizenAsync(slug, accountId, request, cancellationToken);
        }

        await RefreshQuestsAsync(villageId, slug, cancellationToken);
        return created;
    }

    private async Task<CitizenDto> AddCitizenAsync(
        string slug,
        Guid accountId,
        JoinVillageRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var village = await LoadVillageAsync(db, slug, cancellationToken);
        if (village.OwnerAccountId == accountId)
        {
            throw new GameRuleException("citizen.tsar", "The Tsar rules the village and cannot live in it as a citizen.");
        }

        if (await db.Citizens.AnyAsync(c => c.VillageId == village.Id && c.AccountId == accountId, cancellationToken))
        {
            throw new GameRuleException("citizen.alreadyLives", "You already live in this village.");
        }

        if (!village.Admits(request.InviteCode))
        {
            throw string.IsNullOrWhiteSpace(request.InviteCode)
                ? new GameRuleException("village.inviteRequired", "This village is private. Ask the Tsar for the invite code.")
                : new GameRuleException("village.inviteInvalid", "This invite code is wrong or no longer valid.");
        }

        var character = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => a.Character)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new GameRuleException("character.missing", "Create your character first.");

        var now = time.GetUtcNow();
        var citizen = CharacterCreation.Create(village.Id, accountId, character, now);
        db.Citizens.Add(citizen);
        var housed = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .ToListAsync(cancellationToken);
        ShelterRules.Assign(village, [.. housed, citizen], now);
        await db.SaveChangesAsync(cancellationToken);
        logger.CitizenJoined(accountId, village.Slug, citizen.Id);
        presence.Touch(citizen.Id, village.Slug);

        await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
        return citizen.ToDto(village, now);
    }

    /// <summary>The citizen leaves the village for good; see <see cref="Departure.Leave"/>. Their chat lines go with them.</summary>
    public async Task LeaveVillageAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        if (key.IsDue)
        {
            await SettleVillageAsync(key.VillageId, [key.Id], cancellationToken);
        }

        await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var run = citizen.DungeonRunId is { } runId
                ? await db.DungeonRuns.SingleOrDefaultAsync(r => r.Id == runId, cancellationToken)
                : null;
            var now = time.GetUtcNow();
            village.EnsureCamp(now);
            Departure.Leave(citizen, village, run, now);

            await db.ChatMessages.Where(m => m.CitizenId == citizen.Id).ExecuteDeleteAsync(cancellationToken);
            db.Citizens.Remove(citizen);
            var housed = await db.Citizens
                .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active && c.Id != citizen.Id)
                .ToListAsync(cancellationToken);
            ShelterRules.Assign(village, housed, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenLeft(accountId, slug, citizen.Id);
            presence.Forget(citizen.Id);

            await notifier.CitizenLeft(slug, citizen.Id);
            changes.Mark(village.Id);
            return true;
        }, cancellationToken);

        await RefreshQuestsAsync(key.VillageId, slug, cancellationToken);
    }

    /// <summary>
    /// The Tsar deletes the village with its citizens, quests, projects, runs and chat. Not while heroes are away in a
    /// dungeon: their fight runs in memory and comes back to the village.
    /// </summary>
    public async Task DeleteVillageAsync(string slug, Guid accountId, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            if (await db.DungeonRuns.AnyAsync(
                    r => r.VillageId == village.Id && (r.Phase == DungeonPhase.Marching || r.Phase == DungeonPhase.Running),
                    cancellationToken))
            {
                throw new GameRuleException("village.heroesAway", "Wait until the heroes are back from the dungeon.");
            }

            await db.Villages.Where(v => v.Id == village.Id).ExecuteDeleteAsync(cancellationToken);
            logger.VillageDeleted(accountId, slug);

            await notifier.VillageDeleted(slug);
            return true;
        }, cancellationToken);

    /// <summary>A newcomer can leave the tribe short of beds.</summary>
    private Task RefreshQuestsAsync(Guid villageId, string slug, CancellationToken cancellationToken) =>
        WithVillageAsync(villageId, async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            var now = time.GetUtcNow();
            var quests = await UpdateQuestsAsync(db, village, null, null, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var quest in quests)
            {
                logger.QuestCompleted(village.Slug, quest.Kind.ToString());
                await notifier.QuestCompleted(village.Slug, quest);
            }

            changes.Mark(village.Id);
            return true;
        }, cancellationToken);

    public async Task<CitizenDto> StartTaskAsync(string slug, Guid accountId, StartTaskRequest request, CancellationToken cancellationToken)
    {
        var task = GameCatalog.FindTask(request.TaskId)
                   ?? throw new GameRuleException("task.unknown", $"Unknown task \"{request.TaskId}\".",
                       new Dictionary<string, object> { ["taskId"] = request.TaskId });
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await StartAsync(
            key,
            "startTask",
            $"{request.TaskId} {request.Spot}",
            (citizen, context, now) => CycleEngine.Start(citizen, task, context, now, request.Spot),
            cancellationToken);
    }

    /// <summary>The citizen researches the technology the village voted for, at its building.</summary>
    public async Task<CitizenDto> StartResearchAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await StartAsync(key, "startResearch", "", CycleEngine.StartResearch, cancellationToken);
    }

    /// <summary>A citizen votes in the open poll; the poll closes once more than half of the active citizens voted.</summary>
    public async Task<VillageStateDto> VoteAsync(string slug, Guid accountId, string technologyId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            TechnologyRules.CastVote(village, citizen, technologyId, await CountElectorateAsync(db, village, cancellationToken), now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, village.Slug, "vote", technologyId);

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>The Tsar or a Governor puts some available technologies to the vote of the citizens.</summary>
    public async Task<VillageStateDto> OpenPollAsync(string slug, Guid accountId, IReadOnlyList<string> technologyIds, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            var ruler = await RequireRulerAsync(db, village, accountId, cancellationToken);

            var now = time.GetUtcNow();
            TechnologyRules.OpenPoll(village, technologyIds, await CountActiveCitizensAsync(db, village, cancellationToken), now);
            await db.SaveChangesAsync(cancellationToken);
            logger.RulerActed(slug, ruler, "openPoll", string.Join(", ", technologyIds));

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);

    public Task<VillageStateDto> CancelPollAsync(string slug, Guid accountId, CancellationToken cancellationToken) =>
        PlanAsync(slug, accountId, "cancelPoll", "", TechnologyRules.CancelPoll, cancellationToken);

    private static Task<int> CountActiveCitizensAsync(DesertodDbContext db, Village village, CancellationToken cancellationToken) =>
        db.Citizens.CountAsync(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active, cancellationToken);

    /// <summary>Active citizens who are present; away citizens do not hold a vote open.</summary>
    private async Task<int> CountElectorateAsync(DesertodDbContext db, Village village, CancellationToken cancellationToken)
    {
        var ids = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
        return ids.Count(presence.IsPresent);
    }

    public async Task<CitizenDto> StartJobAsync(string slug, Guid accountId, StartJobRequest request, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await StartAsync(
            key,
            "startJob",
            $"{request.JobId} at {request.PlotId} stage {request.Stage}",
            (citizen, context, now) => CycleEngine.StartJob(citizen, request.PlotId, request.Stage, request.JobId, context, now),
            cancellationToken);
    }

    /// <summary>Settles the finished cycle, then starts the next task or job in the same transaction.</summary>
    private Task<CitizenDto> StartAsync(
        CitizenKey key,
        string action,
        string detail,
        Action<Citizen, WorkContext, DateTimeOffset> start,
        CancellationToken cancellationToken) =>
        WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            village.EnsureCamp(now);
            var context = (await LoadContextAsync(db, village, citizen.Id, now, cancellationToken)).Work;
            var raids = await FightDueRaidsAsync(db, village, context.CompletedProjects, now, cancellationToken);

            var report = CycleEngine.Settle(citizen, context, now);
            await AfterSettleAsync(db, village, report, now, cancellationToken);
            var completion = await CompleteProjectIfDoneAsync(db, context, now, cancellationToken);
            if (completion is not null)
            {
                context = (await LoadContextAsync(db, village, citizen.Id, now, cancellationToken)).Work;
            }

            var quests = await UpdateQuestsAsync(db, village, citizen, report, now, cancellationToken);
            start(citizen, context, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, village.Slug, action, detail);

            await BroadcastRaidsAsync(village, raids, now);
            await BroadcastEventsAsync(village, citizen, report, completion, quests, now);
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);

    /// <summary>A Synoptic who has just slept announces the weather of the next day or night.</summary>
    public async Task<VillageStateDto> ForecastAsync(string slug, Guid accountId, bool rain, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            WeatherRules.Forecast(village, citizen, rain, Random.Shared, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, village.Slug, "forecast", rain ? "rain" : "dry");

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>The Tsar or a Governor plans a building. Citizens do all the work.</summary>
    public Task<VillageStateDto> PlaceBlueprintAsync(
        string slug,
        Guid accountId,
        PlaceBlueprintRequest request,
        CancellationToken cancellationToken) =>
        PlanAsync(
            slug,
            accountId,
            "placeBlueprint",
            $"{request.Kind} at {request.X},{request.Y}",
            (village, now) => ConstructionRules.Place(village, request.Kind, request.X, request.Y, now),
            cancellationToken);

    /// <summary>The Tsar walks to a spot on the land; every viewer sees them go.</summary>
    public Task<VillageStateDto> TsarGoToAsync(string slug, Guid accountId, GoToRequest request, CancellationToken cancellationToken) =>
        RuleAsync(slug, accountId, "goTo", request.Spot.ToString(), (village, now) => village.TsarGoTo(request.Spot, now), cancellationToken);

    public Task<VillageStateDto> CancelBlueprintAsync(string slug, Guid accountId, string plotId, CancellationToken cancellationToken) =>
        PlanAsync(slug, accountId, "cancelBlueprint", plotId, (village, _) => ConstructionRules.Cancel(village, plotId), cancellationToken);

    /// <summary>The Tsar writes a subtitle under a citizen's name on the map.</summary>
    public Task<CitizenSummaryDto> SetSubtitleAsync(
        string slug,
        Guid accountId,
        Guid citizenId,
        string? subtitle,
        CancellationToken cancellationToken) =>
        ChangeCitizenAsync(slug, accountId, citizenId, "setSubtitle", citizen => citizen.SetSubtitle(subtitle), cancellationToken);

    /// <summary>The Tsar appoints a citizen Governor, or dismisses them.</summary>
    public Task<CitizenSummaryDto> SetGovernorAsync(
        string slug,
        Guid accountId,
        Guid citizenId,
        bool isGovernor,
        CancellationToken cancellationToken) =>
        ChangeCitizenAsync(
            slug,
            accountId,
            citizenId,
            isGovernor ? "appointGovernor" : "dismissGovernor",
            citizen => citizen.IsGovernor = isGovernor,
            cancellationToken);

    /// <summary>A change the Tsar makes to one citizen of the village.</summary>
    private Task<CitizenSummaryDto> ChangeCitizenAsync(
        string slug,
        Guid accountId,
        Guid citizenId,
        string actionName,
        Action<Citizen> change,
        CancellationToken cancellationToken) =>
        WithRetryAsync(async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            var citizen = await db.Citizens.SingleOrDefaultAsync(c => c.Id == citizenId && c.VillageId == village.Id, cancellationToken)
                          ?? throw new NotFoundException("citizen.notFound", "This citizen does not live in the village.");
            change(citizen);
            await db.SaveChangesAsync(cancellationToken);
            logger.TsarActed(slug, actionName, citizenId.ToString());

            var summary = citizen.ToSummaryDto(time.GetUtcNow());
            await notifier.CitizenUpdated(village.Slug, summary);
            return summary;
        }, cancellationToken);

    /// <summary>An action only the Tsar may take.</summary>
    private async Task<VillageStateDto> RuleAsync(
        string slug,
        Guid accountId,
        string actionName,
        string detail,
        Action<Village, DateTimeOffset> action,
        CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            var state = await ActAsync(db, village, action, cancellationToken);
            logger.TsarActed(slug, actionName, detail);
            return state;
        }, cancellationToken);

    /// <summary>An action the Tsar and the Governors may take: planning buildings and calling votes.</summary>
    private async Task<VillageStateDto> PlanAsync(
        string slug,
        Guid accountId,
        string actionName,
        string detail,
        Action<Village, DateTimeOffset> action,
        CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            var ruler = await RequireRulerAsync(db, village, accountId, cancellationToken);
            var state = await ActAsync(db, village, action, cancellationToken);
            logger.RulerActed(slug, ruler, actionName, detail);
            return state;
        }, cancellationToken);

    private async Task<VillageStateDto> ActAsync(
        DesertodDbContext db,
        Village village,
        Action<Village, DateTimeOffset> action,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        village.EnsureCamp(now);
        village.Explore(await LoadTripsAsync(db, village.Id, cancellationToken), now);
        action(village, now);
        await db.SaveChangesAsync(cancellationToken);

        changes.Mark(village.Id);
        return await BuildStateAsync(db, village, now, cancellationToken);
    }

    /// <summary>The Tsar, or an active citizen the Tsar appointed Governor. Returns "Tsar" or the Governor's citizen id, for the log.</summary>
    private static async Task<string> RequireRulerAsync(DesertodDbContext db, Village village, Guid accountId, CancellationToken cancellationToken)
    {
        if (village.OwnerAccountId == accountId)
        {
            return "Tsar";
        }

        var governorId = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.AccountId == accountId && c.IsGovernor && c.Status == CitizenStatus.Active)
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return governorId is { } id ? $"Governor {id}" : throw new UnauthorizedAccessException();
    }

    /// <summary>A finished shelter changes who sleeps where.</summary>
    private static async Task AfterSettleAsync(
        DesertodDbContext db,
        Village village,
        CycleReport? report,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (report?.BuildingCompleted is not { } kind || BuildingCatalog.Get(kind).Shelter == 0)
        {
            return;
        }

        var citizens = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .ToListAsync(cancellationToken);
        ShelterRules.Assign(village, citizens, now);
    }

    /// <summary>The citizen throws away goods that do not fit in storage, to get back to the same job.</summary>
    public async Task<CitizenDto> DropLoadAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            StorageRules.Drop(citizen, village);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "dropLoad", "");

            var now = time.GetUtcNow();
            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>The citizen drinks a vigor drink; see <see cref="VigorDrinks.Drink"/>.</summary>
    public async Task<CitizenDto> DrinkAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            VigorDrinks.Drink(citizen, village, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "drink", "");

            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>The wounded citizen uses a remedy; see <see cref="CombatRules.Heal"/>.</summary>
    public async Task<CitizenDto> HealAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            var raids = await FightDueRaidsAsync(db, village, null, now, cancellationToken);
            CombatRules.Heal(citizen, village, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "heal", "");

            await BroadcastRaidsAsync(village, raids, now);
            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>The Tsar calls the dead up for the viewers: waves of zombies or skeletons attack the camp.</summary>
    public async Task<VillageStateDto> CallUndeadAsync(string slug, Guid accountId, CallUndeadRequest request, CancellationToken cancellationToken) =>
        await WithVillageAsync(await FindVillageIdAsync(slug, cancellationToken), async db =>
        {
            var village = await LoadVillageAsync(db, slug, cancellationToken);
            if (village.OwnerAccountId != accountId)
            {
                throw new UnauthorizedAccessException();
            }

            var now = time.GetUtcNow();
            RaidRules.CallUndead(village, request.Enemy, request.Size, await CountRecentCitizensAsync(db, village.Id, now, cancellationToken), now);
            await db.SaveChangesAsync(cancellationToken);
            logger.TsarActed(slug, "callUndead", $"{request.Size} {request.Enemy}");

            changes.Mark(village.Id);
            return await BuildStateAsync(db, village, now, cancellationToken);
        }, cancellationToken);

    public Task<VillageStateDto> CancelUndeadAsync(string slug, Guid accountId, CancellationToken cancellationToken) =>
        RuleAsync(slug, accountId, "cancelUndead", "", (village, _) => RaidRules.CancelUndead(village), cancellationToken);

    /// <summary>Villages with a raid due, or whose beasts have not decided yet about tonight.</summary>
    public async Task<IReadOnlyList<Guid>> FindRaidVillagesAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var period = DayCycle.PeriodOf(now);
        var night = DayCycle.IsNight(period);
        var recentSince = now - GameRules.RecentActivityWindow;
        return await db.Villages
            .Where(v => v.NextRaidAt <= now
                        || (night && v.RaidsPlannedPeriod < period
                                  && db.Citizens.Any(c => c.VillageId == v.Id && c.CycleStartedAt >= recentSince)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Plans tonight's raid of a village and fights the raids that struck.</summary>
    public Task RaidVillageAsync(Guid villageId, CancellationToken cancellationToken) =>
        WithVillageAsync(villageId, async db =>
        {
            var village = await db.Villages.SingleAsync(v => v.Id == villageId, cancellationToken);
            var now = time.GetUtcNow();
            var planned = RaidRules.PlanNight(village, await CountRecentCitizensAsync(db, villageId, now, cancellationToken), Random.Shared, now);
            var raids = await FightDueRaidsAsync(db, village, null, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (planned is not null)
            {
                logger.RaidPlanned(village.Slug, planned.Count, planned.Enemy.ToString(), planned.StrikesAt);
            }

            await BroadcastRaidsAsync(village, raids, now);
            if (planned is not null || raids.Reports.Count > 0)
            {
                changes.Mark(villageId);
            }

            return true;
        }, cancellationToken);

    private static Task<int> CountRecentCitizensAsync(DesertodDbContext db, Guid villageId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var recentSince = now - GameRules.RecentActivityWindow;
        return db.Citizens.CountAsync(
            c => c.VillageId == villageId && c.Status == CitizenStatus.Active && c.CycleStartedAt >= recentSince, cancellationToken);
    }

    /// <param name="Citizens">Defenders and wounded, to send their new state.</param>
    private sealed record RaidsFought(IReadOnlyList<RaidReport> Reports, IReadOnlyList<Citizen> Citizens);

    /// <summary>
    /// Fights the raids that struck by now, before anything else changes the citizens: who was on watch at the moment of
    /// the strike is read from their running cycles.
    /// </summary>
    /// <param name="completedProjects">Loaded here when null.</param>
    private static async Task<RaidsFought> FightDueRaidsAsync(
        DesertodDbContext db,
        Village village,
        IReadOnlyCollection<string>? completedProjects,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var due = RaidRules.Due(village, now);
        if (due.Count == 0)
        {
            return new RaidsFought([], []);
        }

        completedProjects ??= await db.VillageProjects
            .Where(p => p.VillageId == village.Id && p.Status == ProjectStatus.Completed)
            .Select(p => p.DefinitionId)
            .ToListAsync(cancellationToken);
        var citizens = await db.Citizens.Where(c => c.VillageId == village.Id).ToListAsync(cancellationToken);
        var reports = due.Select(raid => RaidRules.Resolve(village, raid, citizens, completedProjects, Random.Shared, now)).ToList();
        var affected = reports
            .SelectMany(r => r.Defenders.Select(d => d.CitizenId).Concat(r.Victims.Select(v => v.CitizenId)))
            .ToHashSet();
        return new RaidsFought(reports, citizens.Where(c => affected.Contains(c.Id)).ToList());
    }

    private async Task BroadcastRaidsAsync(Village village, RaidsFought raids, DateTimeOffset now)
    {
        foreach (var report in raids.Reports)
        {
            logger.RaidFought(village.Slug, report.Count, report.Enemy.ToString(), report.Repelled, report.Defenders.Count);
            await notifier.RaidFought(village.Slug, report);
        }

        foreach (var citizen in raids.Citizens)
        {
            await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
        }
    }

    /// <summary>The idle citizen takes a good out of one storage building into their hands.</summary>
    public async Task<CitizenDto> TakeFromStorageAsync(string slug, Guid accountId, TakeRequest request, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            StorageRules.Take(citizen, village, request.PlotId, request.Resource, request.Amount, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "take", $"{request.Amount} {request.Resource} from {request.PlotId}");

            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>The citizen stops the running task, job or research; see <see cref="CycleEngine.Cancel"/>.</summary>
    public async Task<CitizenDto> CancelTaskAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            var raids = await FightDueRaidsAsync(db, village, null, now, cancellationToken);
            CycleEngine.Cancel(citizen, now, village);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "cancelTask", "");

            await BroadcastRaidsAsync(village, raids, now);
            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            changes.Mark(village.Id);
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    /// <summary>"Come here": the idle citizen walks to a spot on the land and waits there for the next task.</summary>
    public async Task<CitizenDto> GoToAsync(string slug, Guid accountId, GoToRequest request, CancellationToken cancellationToken)
    {
        var key = await RequireCitizenAsync(slug, accountId, cancellationToken);
        return await WithVillageAsync(key.VillageId, async db =>
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == key.Id, cancellationToken);
            var village = await db.Villages.SingleAsync(v => v.Id == citizen.VillageId, cancellationToken);
            var now = time.GetUtcNow();
            village.EnsureCamp(now);
            citizen.GoTo(request.Spot, village, now);
            await db.SaveChangesAsync(cancellationToken);
            logger.CitizenActed(citizen.Id, slug, "goTo", request.Spot.ToString());

            await notifier.CitizenUpdated(slug, citizen.ToSummaryDto(now));
            return citizen.ToDto(village, now);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DueCitizen>> FindDueCitizensAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        return await db.Citizens
            .Where(c => !c.CycleSettled && c.CycleEndsAt <= now)
            .OrderBy(c => c.CycleEndsAt)
            .Select(c => new DueCitizen(c.VillageId, c.Id))
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Banks the finished cycles of some citizens of one village, one by one, then marks the village state for sending once.
    /// The village lock is taken per <see cref="SettlementChunkSize"/> citizens, so player actions wait for one chunk,
    /// not for the whole batch.
    /// </summary>
    public async Task SettleVillageAsync(Guid villageId, IReadOnlyCollection<Guid> citizenIds, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var settled = 0;
        foreach (var chunk in citizenIds.Chunk(SettlementChunkSize))
        {
            using var _ = await AcquireLockAsync(villageId, cancellationToken);
            var remaining = new Queue<Guid>(chunk);
            // A conflict spoils the context; the retry goes on with the citizens not saved yet.
            await WithRetryAsync(async db =>
            {
                var village = await db.Villages.SingleAsync(v => v.Id == villageId, cancellationToken);
                while (remaining.TryPeek(out var citizenId))
                {
                    if (await SettleOneAsync(db, village, citizenId, cancellationToken))
                    {
                        settled++;
                    }

                    remaining.Dequeue();
                }

                return true;
            }, cancellationToken);
        }

        if (settled > 0)
        {
            changes.Mark(villageId);
        }

        logger.VillageSettled(settled, citizenIds.Count, villageId, (long)time.GetElapsedTime(started).TotalMilliseconds);
    }

    /// <summary>The village state for viewers, or null when the village is gone.</summary>
    public async Task<VillageStateDto?> GetVillageStateAsync(Guid villageId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var village = await db.Villages.AsNoTracking().SingleOrDefaultAsync(v => v.Id == villageId, cancellationToken);
        return village is null ? null : await BuildStateAsync(db, village, time.GetUtcNow(), cancellationToken);
    }

    private async Task<bool> SettleOneAsync(DesertodDbContext db, Village village, Guid citizenId, CancellationToken cancellationToken)
    {
        var citizen = await db.Citizens.SingleAsync(c => c.Id == citizenId, cancellationToken);
        if (citizen.CycleSettled || citizen.VillageId != village.Id)
        {
            return false;
        }

        var now = time.GetUtcNow();
        village.EnsureCamp(now);
        var context = (await LoadContextAsync(db, village, citizen.Id, now, cancellationToken)).Work;
        var raids = await FightDueRaidsAsync(db, village, context.CompletedProjects, now, cancellationToken);
        var report = CycleEngine.Settle(citizen, context, now);
        if (report is null)
        {
            if (raids.Reports.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                await BroadcastRaidsAsync(village, raids, now);
                changes.Mark(village.Id);
            }

            return false;
        }

        await AfterSettleAsync(db, village, report, now, cancellationToken);
        var completion = await CompleteProjectIfDoneAsync(db, context, now, cancellationToken);
        var quests = await UpdateQuestsAsync(db, village, citizen, report, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await BroadcastRaidsAsync(village, raids, now);
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.CycleSettled(
                citizen.Id,
                village.Slug,
                report.TaskId,
                FormatAmounts(report.Produced),
                FormatAmounts(report.Carried),
                report.ProjectContribution,
                report.RareFind);
        }

        await BroadcastEventsAsync(village, citizen, report, completion, quests, now);
        return true;
    }

    private static string FormatAmounts(IEnumerable<ResourceAmount> amounts) =>
        string.Join(", ", amounts.Select(amount => $"{amount.Amount} {amount.Resource}"));

    private async Task<ProjectCompletion?> CompleteProjectIfDoneAsync(
        DesertodDbContext db,
        WorkContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.Project is not { Status: ProjectStatus.Active, IsComplete: true } project)
        {
            return null;
        }

        var contributorIds = project.Contributions.Select(c => c.CitizenId).Distinct().ToList();
        var contributors = await db.Citizens
            .Where(c => contributorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return ProjectRules.Complete(project, context.Village, contributors, now);
    }

    /// <summary>
    /// Credits the finished cycle, opens quests for the village's current problems and completes the solved ones.
    /// <paramref name="citizen"/> is the one being settled: its hunger is not saved yet, so it is counted from memory.
    /// </summary>
    private async Task<IReadOnlyList<QuestCompletion>> UpdateQuestsAsync(
        DesertodDbContext db,
        Village village,
        Citizen? citizen,
        CycleReport? report,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var active = await db.VillageQuests
            .Include(q => q.Contributions)
            .Where(q => q.VillageId == village.Id && q.Status == QuestStatus.Active)
            .ToListAsync(cancellationToken);

        var settlingId = citizen?.Id ?? Guid.Empty;
        var counts = await db.Citizens
            .Where(c => c.VillageId == village.Id && c.Status == CitizenStatus.Active)
            .GroupBy(_ => 1)
            .Select(g => new { Active = g.Count(), OtherHungry = g.Count(c => c.IsHungry && c.Id != settlingId) })
            .SingleOrDefaultAsync(cancellationToken);
        var needs = new VillageNeeds(
            counts?.Active ?? 0,
            (counts?.OtherHungry ?? 0) + (citizen is { IsHungry: true } ? 1 : 0));

        if (report is not null)
        {
            QuestRules.Credit(village, active, report);
        }

        foreach (var opened in QuestRules.Open(village, active, needs, report, now))
        {
            logger.QuestOpened(village.Slug, opened.Kind.ToString());
            db.VillageQuests.Add(opened);
        }

        var storyDone = await db.VillageQuests
            .Where(q => q.VillageId == village.Id && q.Status == QuestStatus.Completed)
            .Select(q => q.Kind)
            .Distinct()
            .ToListAsync(cancellationToken);
        var story = StoryQuests.Open(village, active, storyDone, now);
        if (story is not null)
        {
            logger.QuestOpened(village.Slug, story.Kind.ToString());
            db.VillageQuests.Add(story);
        }

        // Tracked entities first: a project finished in this transaction is not saved yet.
        var projects = await db.VillageProjects
            .Where(p => p.VillageId == village.Id)
            .ToListAsync(cancellationToken);
        var completedProjects = projects.Where(p => p.Status == ProjectStatus.Completed).Select(p => p.DefinitionId).ToHashSet();
        var running = projects.SingleOrDefault(p => p.Status == ProjectStatus.Active);
        var storyQuests = story is null ? active : [.. active, story];
        if (StoryQuests.ProjectToStart(storyQuests, running, completedProjects) is { } project)
        {
            var started = VillageProject.Start(village.Id, project, now);
            logger.ProjectStarted(village.Slug, started.DefinitionId);
            db.VillageProjects.Add(started);
        }

        var solved = active.Where(q => QuestRules.IsComplete(q, village, needs.ActiveCitizens, completedProjects)).ToList();
        if (solved.Count == 0)
        {
            return [];
        }

        var contributorIds = solved.SelectMany(q => q.Contributions).Select(c => c.CitizenId).Distinct().ToList();
        var contributors = await db.Citizens
            .Where(c => contributorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        return solved.Select(q => QuestRules.Complete(q, village, contributors, now)).ToList();
    }

    /// <summary>Everything about one citizen's cycle except the village state, which callers send once.</summary>
    private async Task BroadcastEventsAsync(
        Village village,
        Citizen citizen,
        CycleReport? report,
        ProjectCompletion? completion,
        IReadOnlyList<QuestCompletion> quests,
        DateTimeOffset now)
    {
        if (report is not null)
        {
            await notifier.CycleCompleted(village.Slug, report);
        }

        if (completion is not null)
        {
            logger.ProjectCompleted(village.Slug, completion.ProjectId);
            await notifier.ProjectCompleted(village.Slug, completion);
        }

        foreach (var quest in quests)
        {
            logger.QuestCompleted(village.Slug, quest.Kind.ToString());
            await notifier.QuestCompleted(village.Slug, quest);
        }

        await notifier.CitizenUpdated(village.Slug, citizen.ToSummaryDto(now));
    }

    private static async Task<VillageContext> LoadContextAsync(
        DesertodDbContext db,
        Village village,
        Guid? citizenId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var project = await db.VillageProjects
            .Include(p => p.Materials)
            .Include(p => p.Contributions)
            .SingleOrDefaultAsync(p => p.VillageId == village.Id && p.Status == ProjectStatus.Active, cancellationToken);

        var completedProjects = await db.VillageProjects
            .Where(p => p.VillageId == village.Id && p.Status == ProjectStatus.Completed)
            .OrderBy(p => p.CompletedAt)
            .Select(p => p.DefinitionId)
            .ToListAsync(cancellationToken);

        // Active citizens, plus anyone still in a cycle whatever their status: working counts include them.
        var citizens = await db.Citizens
            .Where(c => c.VillageId == village.Id
                        && (c.Status == CitizenStatus.Active || (!c.CycleSettled && c.CycleEndsAt > now)))
            .Select(c => new
            {
                c.Id,
                IsActive = c.Status == CitizenStatus.Active,
                c.CycleStartedAt,
                IsWorking = !c.CycleSettled && c.CycleEndsAt > now,
                c.CurrentTaskId,
                c.Stats.Intelligence,
            })
            .ToListAsync(cancellationToken);

        var recentSince = now - GameRules.RecentActivityWindow;
        var working = citizens.Where(c => c.IsWorking).ToList();
        var work = new WorkContext
        {
            Village = village,
            Project = project,
            CompletedProjects = completedProjects.ToHashSet(),
            ActiveCitizenCount = citizens.Count(c => c.IsActive),
            RecentCitizenCount = Math.Max(1, citizens.Count(c => c.IsActive && c.CycleStartedAt >= recentSince)),
            SupervisionCapacity = working
                .Where(c => c.CurrentTaskId == SuperviseTaskId)
                .Sum(c => ProjectRules.SupervisionCapacity(c.Intelligence)),
            BuildersWorking = working.Count(c => c.CurrentTaskId == BuildTaskId && c.Id != citizenId),
        };
        return new VillageContext(work, completedProjects, citizens.Where(c => c.IsActive).Select(c => c.Id).ToList());
    }

    private async Task<VillageStateDto> BuildStateAsync(
        DesertodDbContext db,
        Village village,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var context = await LoadContextAsync(db, village, null, now, cancellationToken);
        var electorate = context.ActiveCitizenIds.Count(presence.IsPresent);
        var quests = await db.VillageQuests
            .Where(q => q.VillageId == village.Id && q.Status == QuestStatus.Active)
            .OrderBy(q => q.StartedAt)
            .ToListAsync(cancellationToken);
        var owner = await db.Accounts.AsNoTracking()
            .Where(a => a.Id == village.OwnerAccountId)
            .Select(a => new { a.DisplayName, a.Character })
            .SingleAsync(cancellationToken);
        var tsar = new TsarDto(owner.Character?.Name ?? owner.DisplayName, owner.Character?.Appearance, village.TsarSpot);
        var trips = await LoadTripsAsync(db, village.Id, cancellationToken);
        var shownSince = now - DungeonRules.ShownAfterEnd;
        var dungeon = await db.DungeonRuns.AsNoTracking()
            .Where(r => r.VillageId == village.Id && (r.Phase != DungeonPhase.Ended || r.EndedAt >= shownSince))
            .OrderByDescending(r => r.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return village.ToStateDto(context.Work, context.CompletedInOrder, electorate, quests, tsar, trips, dungeon, now);
    }

    /// <summary>Last walks of the village's citizens, those who left included: what they saw stays seen.</summary>
    private static async Task<IReadOnlyList<Trip>> LoadTripsAsync(DesertodDbContext db, Guid villageId, CancellationToken cancellationToken) =>
        await db.Citizens.AsNoTracking()
            .Where(c => c.VillageId == villageId && c.Trip != null)
            .Select(c => c.Trip!)
            .ToListAsync(cancellationToken);

    /// <summary>Runs a change to the village while no other change to it runs in this process.</summary>
    private async Task<T> WithVillageAsync<T>(Guid villageId, Func<DesertodDbContext, Task<T>> action, CancellationToken cancellationToken)
    {
        using var _ = await AcquireLockAsync(villageId, cancellationToken);
        return await WithRetryAsync(action, cancellationToken);
    }

    private async Task<IDisposable> AcquireLockAsync(Guid villageId, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var releaser = await locks.AcquireAsync(villageId, cancellationToken);
        var waited = time.GetElapsedTime(started);
        if (waited >= SlowLockThreshold)
        {
            logger.SlowLock((long)waited.TotalMilliseconds, villageId);
        }

        return releaser;
    }

    private async Task<T> WithRetryAsync<T>(Func<DesertodDbContext, Task<T>> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            try
            {
                return await action(db);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaximumAttempts)
            {
                logger.Retrying(attempt, MaximumAttempts, "a concurrency conflict");
                await Task.Delay(Random.Shared.Next(10, 50) * attempt, cancellationToken);
            }
            catch (DbUpdateException exception) when (attempt < MaximumAttempts
                                                      && exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two cycles opened the same quest at once; the retry finds the one that was saved.
                logger.Retrying(attempt, MaximumAttempts, "a unique violation");
                await Task.Delay(Random.Shared.Next(10, 50) * attempt, cancellationToken);
            }
        }
    }

    private async Task<CitizenKey?> FindCitizenAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        return await db.Citizens
            .Where(c => c.AccountId == accountId && db.Villages.Any(v => v.Id == c.VillageId && v.Slug == slug))
            .Select(c => new CitizenKey(c.Id, c.VillageId, !c.CycleSettled && c.CycleEndsAt <= now))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>The player's citizen in the village, who is present from now on since they act.</summary>
    private async Task<CitizenKey> RequireCitizenAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var key = await FindCitizenAsync(slug, accountId, cancellationToken)
                  ?? throw new NotFoundException("citizen.notInVillage", "You do not live in this village.");
        await SeeAsync(slug, key.Id);
        return key;
    }

    private async Task<Guid> FindVillageIdAsync(string slug, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Villages.Where(v => v.Slug == slug).Select(v => (Guid?)v.Id).SingleOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("village.notFound", $"Village \"{slug}\" was not found.", new Dictionary<string, object> { ["slug"] = slug });
    }

    private static async Task<Village> LoadVillageAsync(DesertodDbContext db, string slug, CancellationToken cancellationToken) =>
        await db.Villages.SingleOrDefaultAsync(v => v.Slug == slug, cancellationToken)
        ?? throw new NotFoundException("village.notFound", $"Village \"{slug}\" was not found.", new Dictionary<string, object> { ["slug"] = slug });

    private static string Slugify(string name)
    {
        var builder = new StringBuilder();
        foreach (var character in Transliterate(name.ToLowerInvariant()))
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > 40)
        {
            slug = slug[..40].Trim('-');
        }

        return slug.Length == 0 ? "village" : slug;
    }

    private static string Transliterate(string text)
    {
        const string cyrillic = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        string[] latin =
        [
            "a", "b", "v", "g", "d", "e", "e", "zh", "z", "i", "y", "k", "l", "m", "n", "o", "p", "r", "s", "t", "u",
            "f", "h", "ts", "ch", "sh", "sch", "", "y", "", "e", "yu", "ya",
        ];

        var builder = new StringBuilder();
        foreach (var character in text)
        {
            var index = cyrillic.IndexOf(character);
            builder.Append(index >= 0 ? latin[index] : character.ToString());
        }

        return builder.ToString();
    }
}
