using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Projects;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.Weather;
using Desertod.Domain.World;

namespace Desertod.Domain.Rules;

/// <param name="ProjectContribution">Material carried to, or work done on, a project or construction site.</param>
/// <param name="SiteId">Plot of the construction site for construction jobs.</param>
/// <param name="BuildingCompleted">Set when this cycle finished the last stage of a building.</param>
/// <param name="Hungry">No food was left in storage for this cycle.</param>
/// <param name="TechnologyId">Technology the cycle researched.</param>
/// <param name="TechnologyDiscovered">Set when this cycle finished the research of <paramref name="TechnologyId"/>.</param>
/// <param name="LevelsGained">Stat levels; skill levels are in <paramref name="SkillLevelsGained"/>.</param>
public sealed record CycleReport(
    Guid CitizenId,
    string CitizenName,
    string TaskId,
    IReadOnlyList<ResourceAmount> Produced,
    IReadOnlyList<ResourceAmount> Carried,
    double MoraleGained,
    int ProjectContribution,
    bool RareFind,
    Stat Primary,
    int PrimaryExperience,
    Stat Secondary,
    int SecondaryExperience,
    Skill Skill,
    int SkillExperience,
    int SkillLevelsGained,
    int LevelsGained,
    int RenownGained,
    bool Tired,
    bool Overflowed,
    string? SiteId = null,
    BuildingKind? BuildingCompleted = null,
    bool Hungry = false,
    string? TechnologyId = null,
    bool TechnologyDiscovered = false,
    FightReport? Fight = null,
    int Repaired = 0)
{
    /// <summary>Part of <see cref="Produced"/> that fit neither in storage nor in the citizen's hands.</summary>
    public IReadOnlyList<ResourceAmount> Lost { get; init; } = [];
}

public static class CycleEngine
{
    private const double UnloadExperienceMultiplier = 0.5;

    /// <summary>
    /// Starts a new task. A previous cycle must already be settled (<see cref="Settle"/>).
    /// Renewing inside the grace window keeps the streak; inputs are consumed up front.
    /// </summary>
    /// <param name="spot">Where on the map the player wants the goods gathered; see <see cref="WorkSpots"/>.</param>
    public static void Start(Citizen citizen, TaskDefinition task, WorkContext context, DateTimeOffset now, TilePoint? spot = null)
    {
        var village = context.Village;
        CheckReady(citizen, now);
        if (spot is { } picked && !Wilderness.IsWild(task))
        {
            WorkSpots.Check(village, task, picked, now);
        }

        if (task.Effect == TaskEffect.NotAvailableYet)
        {
            throw new GameRuleException("task.notAvailable", $"\"{task.Name}\" is not available yet.", new Dictionary<string, object> { ["taskId"] = task.Id });
        }

        if (task.RequiredBuilding is { } building && !village.Has(building))
        {
            throw new GameRuleException("task.needsBuilding", $"\"{task.Name}\" needs a {BuildingCatalog.Get(building).Name}.",
                new Dictionary<string, object> { ["building"] = building.ToString() });
        }

        if (task.RequiredProject is { } projectId && !context.CompletedProjects.Contains(projectId))
        {
            throw new GameRuleException("task.needsProject", $"\"{task.Name}\" needs the {ProjectCatalog.Find(projectId)?.Name}.",
                new Dictionary<string, object> { ["taskId"] = task.Id, ["project"] = projectId });
        }

        var role = GameCatalog.RoleOf(task);
        foreach (var technology in new[] { task.RequiredTechnology, role.Technology })
        {
            if (!TechnologyRules.Knows(village, technology))
            {
                throw new GameRuleException("task.needsTechnology",
                    $"\"{task.Name}\" needs {TechnologyCatalog.Get(technology!).Name}. The village must discover it first.",
                    new Dictionary<string, object> { ["taskId"] = task.Id, ["technology"] = technology! });
            }
        }

        SkillRules.Check(citizen, task);

        if (task.Effect is TaskEffect.Sow or TaskEffect.Water or TaskEffect.Harvest)
        {
            FarmRules.Check(village, task, now);
        }

        CheckHands(citizen, village, task, now);
        CheckProjectTask(citizen, task, context);
        CheckPrey(village, task, now);
        var repairAt = task.Effect == TaskEffect.Repair
            ? CombatRules.MostDamaged(village) ?? throw new GameRuleException("repair.nothing", "No building needs repair.")
            : null;
        if (GoldVein.IsDug(task) && GoldVein.IsEmpty(village))
        {
            throw new GameRuleException("task.veinEmpty", "The gold vein is empty.", new Dictionary<string, object> { ["taskId"] = task.Id });
        }

        if (Wilderness.IsWild(task))
        {
            spot = Wilderness.WorkSpot(village, task, spot);
        }

        foreach (var input in task.Inputs)
        {
            if (Available(citizen, village, input.Resource) < input.Amount)
            {
                throw new GameRuleException("storage.missingInput", $"Not enough {ResourceNames.Of(input.Resource)} in storage.", Resource(input.Resource));
            }
        }

        foreach (var input in task.Inputs)
        {
            TakeFromHandsThenStorage(citizen, village, input.Resource, input.Amount);
        }

        var repairYard = repairAt is null ? (TilePoint?)null : WorldMap.Yard(repairAt);
        if ((spot ?? repairYard ?? NaturalPlace(task)) is { } destination)
        {
            citizen.SetOut(destination, village, now);
        }

        var work = SkillRules.WorkTime(citizen, task.Skill, task.SkillLevel, TimeSpan.FromSeconds(task.WorkSeconds));
        var walk = repairYard is { } yard ? WorldMap.Walk(WorldMap.Places[WorldMap.Camp], yard) : Walk(citizen, village, task, spot, now);
        Begin(citizen, task.Id, work + walk, WeatherRules.EnergyCost(village, !WeatherRules.IsIndoor(task), now), village, now);
        citizen.CurrentSiteId = null;
        citizen.CurrentSiteStage = null;
        citizen.Spot = spot;
        if (IsArmed(task))
        {
            CombatRules.Arm(citizen, village);
        }
    }

    /// <summary>Tasks a citizen takes weapons for: work where beasts attack, and guard duty.</summary>
    public static bool IsArmed(TaskDefinition task) => task.Danger is not null || task.Effect == TaskEffect.Guard;

    /// <summary>Starts a job on a construction site: hauling material there, or working on the current stage.</summary>
    public static void StartJob(Citizen citizen, string plotId, int stage, string jobId, WorkContext context, DateTimeOffset now)
    {
        var village = context.Village;
        CheckReady(citizen, now);
        var job = ConstructionRules.CheckJob(village, citizen, plotId, stage, jobId, context.RecentCitizenCount);

        var yard = WorldMap.Yard(plotId);
        var duration = job.Kind == JobKind.Haul
            ? SkillRules.WorkTime(citizen, job.Skill, 0, BuildingCatalog.HaulBaseTime) + WorldMap.Walk(
                citizen.Carried(job.Resource!.Value) > 0 ? WorldMap.Places[WorldMap.Camp] : village.PickupPoint(job.Resource.Value, yard),
                yard)
            : SkillRules.WorkTime(citizen, job.Skill, 0, GameRules.CraftLength) + WorldMap.Walk(WorldMap.Places[WorldMap.Camp], yard);

        Begin(citizen, job.Id, duration, WeatherRules.EnergyCost(village, true, now), village, now);
        citizen.CurrentSiteId = plotId;
        citizen.CurrentSiteStage = stage;
    }

    /// <summary>Starts researching the technology the citizens voted for, at its building.</summary>
    public static void StartResearch(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        CheckReady(citizen, now);
        var (technology, plotId) = TechnologyRules.CheckResearch(context.Village, citizen);

        Begin(citizen, TechnologyCatalog.ResearchTaskId,
            SkillRules.WorkTime(citizen, SkillRules.Research, 0, GameRules.CraftLength)
            + WorldMap.Walk(WorldMap.Places[WorldMap.Camp], WorldMap.Yard(plotId)),
            WeatherRules.EnergyCost(context.Village, false, now), context.Village, now);
        citizen.CurrentTechnologyId = technology.Id;
        citizen.CurrentSiteId = plotId;
        citizen.CurrentSiteStage = null;
    }

    /// <summary>
    /// Stops the running task, job or research. Nothing is given back: the inputs and energy spent at the start
    /// are lost and the cycle earns nothing. The streak stays where it was before this cycle started.
    /// </summary>
    /// <param name="village">Takes back the weapons the citizen took for the task; without it they keep them until the next task ends.</param>
    public static void Cancel(Citizen citizen, DateTimeOffset now, Village? village = null)
    {
        if (citizen.WorkState(now) != WorkState.Working)
        {
            throw new GameRuleException("task.notRunning", "There is no task running.");
        }

        if (village is not null)
        {
            CombatRules.Disarm(citizen, village);
        }

        // A streak above zero means this cycle renewed the last one: keep the grace window open so the next task renews it again.
        if (citizen.Streak > 0)
        {
            citizen.Streak--;
            citizen.CycleEndsAt = now;
        }
        else
        {
            citizen.CycleEndsAt = null;
        }

        citizen.CycleSettled = true;
        citizen.CurrentTaskId = null;
        citizen.CurrentTechnologyId = null;
        citizen.CurrentSiteId = null;
        citizen.CurrentSiteStage = null;
        citizen.CycleStartedAt = null;
        citizen.Spot = null;
    }

    /// <summary>Banks the result of a completed cycle. Returns null if there is nothing to settle.</summary>
    public static CycleReport? Settle(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        if (citizen.CycleSettled || citizen.CurrentTaskId is null || citizen.CycleEndsAt is null
            || now < citizen.CycleEndsAt)
        {
            return null;
        }

        citizen.CycleSettled = true;
        if (citizen.CurrentTechnologyId is not null)
        {
            return SettleResearch(citizen, context, now);
        }

        var report = citizen.CurrentSiteId is not null
            ? SettleJob(citizen, context, now)
            : SettleTask(citizen, context, now);
        CombatRules.Disarm(citizen, context.Village);
        return report;
    }

    private static CycleReport? SettleResearch(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        var technology = TechnologyCatalog.Find(citizen.CurrentTechnologyId!);
        if (technology is null)
        {
            return null;
        }

        var village = context.Village;
        var factor = YieldFactor(citizen, village, technology.Primary, technology.Secondary, now) / GameRules.StatFactorBase
                     * SkillRules.YieldMultiplier(citizen, SkillRules.Research, 0)
                     * WeatherRules.ResearchMultiplier(village, WorkedAt(citizen, now));
        var (amount, discovered) = TechnologyRules.Research(village, citizen, technology, factor, context.RecentCitizenCount, now);
        var overflowed = amount == 0;
        var hungry = Eat(citizen, village, now);

        var challenge = Progression.ChallengeMultiplier(citizen.Stats.Get(technology.Primary), technology.Researcher.Max(r => r.Minimum));
        var report = Finish(citizen, village, TechnologyCatalog.ResearchTaskId, technology.Primary, technology.Secondary, SkillRules.Research,
            challenge, overflowed ? GameRules.OverflowExperienceMultiplier : 1, hungry,
            [], [], 0, 0, false, overflowed);
        return report with { TechnologyId = technology.Id, TechnologyDiscovered = discovered };
    }

    private static CycleReport? SettleTask(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        var task = GameCatalog.FindTask(citizen.CurrentTaskId!);
        if (task is null)
        {
            return null;
        }

        var village = context.Village;
        var project = context.Project is { Status: ProjectStatus.Active } active ? active : null;
        var factor = YieldFactor(citizen, village, task.Primary, task.Secondary, now) * TechnologyRules.YieldMultiplier(village, task.Id)
                     * SkillRules.YieldMultiplier(citizen, task.Skill, task.SkillLevel)
                     * WeatherRules.YieldMultiplier(village, task, WorkedAt(citizen, now))
                     * CombatRules.BuildingMultiplier(village, task);

        var produced = new List<ResourceAmount>();
        var carried = new List<ResourceAmount>();
        var lost = new List<ResourceAmount>();
        var overflowed = false;
        var rareFind = false;
        double moraleGained = 0;
        var contributed = 0;
        var repaired = 0;
        double experienceShare = 1;

        void Produce(ResourceKind kind, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            produced.Add(new ResourceAmount(kind, amount));
            Keep(citizen, village, kind, amount, carried, lost);
        }

        var fight = task.Danger is { } danger
            ? CombatRules.Ambush(citizen, village, danger, context.Random, WorkedAt(citizen, now), now)
            : null;
        foreach (var loot in fight?.Loot ?? [])
        {
            Produce(loot.Resource, loot.Amount);
        }

        // Beaten by a beast: the citizen runs back with nothing.
        var beaten = fight is { Won: false };
        overflowed = beaten;
        switch (beaten ? (TaskEffect?)null : task.Effect)
        {
            case TaskEffect.Guard:
                break;

            case TaskEffect.Repair:
                repaired = CombatRules.Repair(village, YieldMath.Whole(task.BaseYield * factor));
                overflowed = repaired == 0;
                break;

            case TaskEffect.Resources:
            {
                var share = factor;
                if (task.Hunt is { } hunt)
                {
                    var herd = village.HerdOf(hunt.Prey);
                    var caught = herd.Take(YieldMath.Whole(hunt.Animals * factor * herd.Abundance(now)), now);
                    share = (double)caught / hunt.Animals;
                    overflowed = caught == 0;
                }

                if (GoldVein.IsDug(task))
                {
                    var perShare = task.Outputs.Sum(o => o.Amount);
                    var dug = GoldVein.Take(village, YieldMath.Whole(perShare * share));
                    share = (double)dug / perShare;
                    overflowed = dug == 0;
                }

                if (Wilderness.ObstacleOf(task) is { } obstacle)
                {
                    var perCycle = Wilderness.WorkPerCycle(obstacle);
                    var cleared = Wilderness.Clear(village, obstacle, YieldMath.Whole(perCycle * share),
                        citizen.Spot ?? WorldMap.Places[WorldMap.Camp], now);
                    share = (double)cleared / perCycle;
                    overflowed = cleared == 0;
                }

                if (task.Workplace == Wilderness.Cave && context.Random.NextDouble() < Wilderness.CaveFindChance * share)
                {
                    var find = Loot.Roll(Wilderness.CaveFinds, context.Random);
                    Produce(find.Resource, find.Amount);
                }

                var wood = 0;
                foreach (var output in task.Outputs)
                {
                    var amount = YieldMath.Whole(output.Amount * share);
                    Produce(output.Resource, amount);
                    if (output.Resource == ResourceKind.Wood)
                    {
                        wood += amount;
                    }
                }

                if (task.Workplace == Forest.Workplace)
                {
                    Forest.Chop(village, wood, now, citizen.Spot);
                }

                break;
            }

            case TaskEffect.Unload:
                produced.AddRange(StorageRules.Unload(citizen, village));
                overflowed = citizen.Load.Count > 0;
                experienceShare = UnloadExperienceMultiplier;
                break;

            case TaskEffect.Sow:
                overflowed = !FarmRules.Sow(village, task.Crop!.Value, now);
                break;

            case TaskEffect.Water:
                overflowed = FarmRules.Water(village, now) == 0;
                break;

            case TaskEffect.Harvest:
            {
                var crop = FarmRules.Harvest(village, now);
                overflowed = crop is null;
                foreach (var output in crop?.Harvest ?? [])
                {
                    Produce(output.Resource, YieldMath.Whole(output.Amount * factor));
                }

                break;
            }

            case TaskEffect.Morale:
                var before = village.MoraleAt(now);
                if (before >= GameRules.MoraleMaximum)
                {
                    overflowed = true;
                }
                else
                {
                    village.AddMorale(GameRules.MoralePerPerformYield * task.BaseYield * factor, now);
                    moraleGained = village.Morale - before;
                }

                break;

            case TaskEffect.Deliver:
            {
                var output = task.Outputs[0];
                var key = output.Resource.ToString();
                var amount = project is null
                    ? 0
                    : Min(YieldMath.Whole(output.Amount * factor), Available(citizen, village, output.Resource), project.Remaining(key),
                        project.ContributionRoom(citizen.Id, key, context.ActiveCitizenCount));

                if (amount <= 0)
                {
                    overflowed = true;
                    break;
                }

                TakeFromHandsThenStorage(citizen, village, output.Resource, amount);
                project!.Record(citizen.Id, key, amount);
                contributed = amount;
                break;
            }

            case TaskEffect.Survey:
            case TaskEffect.Build:
            {
                var key = task.Effect == TaskEffect.Survey ? VillageProject.SurveyKey : VillageProject.BuildKey;
                var amount = 0;
                if (project is not null)
                {
                    var limit = task.Effect == TaskEffect.Survey
                        ? project.Remaining(key)
                        : Math.Max(0, project.BuildLimit - project.BuildProgress);
                    amount = Min(YieldMath.Whole(task.BaseYield * factor), limit,
                        project.ContributionRoom(citizen.Id, key, context.ActiveCitizenCount));
                }

                if (amount <= 0)
                {
                    overflowed = true;
                    break;
                }

                project!.Record(citizen.Id, key, amount);
                contributed = amount;
                break;
            }

            case TaskEffect.Supervise:
                overflowed = project is null || project.Phase != ProjectPhase.Construction || project.BuildDone;
                break;

            case TaskEffect.Expedition:
            {
                var scout = task.RoleId == "scout";
                for (var roll = 0; roll < task.BaseYield; roll++)
                {
                    var loot = Expeditions.Roll(context.Random);
                    Produce(loot.Resource, YieldMath.Whole(loot.Amount * factor));
                }

                if (!citizen.CycleTired
                    && context.Random.NextDouble() < Expeditions.RareChance(citizen.Stats.Dexterity, scout))
                {
                    Produce(Expeditions.RareFind.Resource, Expeditions.RareFind.Amount);
                    rareFind = true;
                }

                break;
            }

            case TaskEffect.Roam:
            {
                var found = new Dictionary<ResourceKind, int>();
                var chance = Roaming.FindChance(citizen.Stats.Dexterity);
                for (var glance = 0; glance < Roaming.Glances; glance++)
                {
                    if (context.Random.NextDouble() < chance)
                    {
                        var loot = Loot.Roll(Roaming.Table, context.Random);
                        found[loot.Resource] = found.GetValueOrDefault(loot.Resource) + loot.Amount;
                    }
                }

                foreach (var (kind, amount) in found)
                {
                    Produce(kind, YieldMath.Whole(amount * factor));
                }

                break;
            }
        }

        if (task.Effect != TaskEffect.Unload)
        {
            experienceShare = overflowed ? GameRules.OverflowExperienceMultiplier : 1;
            if (carried.Count > 0 || lost.Count > 0)
            {
                overflowed = true;
                var storedShare = 1 - (double)(carried.Sum(c => c.Amount) + lost.Sum(l => l.Amount)) / produced.Sum(p => p.Amount);
                experienceShare = GameRules.OverflowExperienceMultiplier + (1 - GameRules.OverflowExperienceMultiplier) * storedShare;
            }
        }

        // Prey differ in hunting time; experience follows the time, so every prey gives the same experience per minute.
        if (task.Hunt is not null)
        {
            experienceShare *= task.WorkSeconds / GameRules.CycleLength.TotalSeconds;
        }

        var hungry = task.Effect != TaskEffect.Unload && Eat(citizen, village, now);
        var report = Finish(citizen, village, task.Id, task.Primary, task.Secondary, task.Skill,
            SkillRules.Challenge(citizen, task.Skill, task.SkillLevel), experienceShare, hungry,
            produced, carried, moraleGained, contributed, rareFind, overflowed);
        return report with { Fight = fight, Repaired = repaired, Lost = lost };
    }

    /// <summary>
    /// Stores what fits; the rest goes to the citizen's hands up to <see cref="StorageRules.TakeLimit"/> of the good,
    /// and anything beyond that is lost.
    /// </summary>
    private static void Keep(Citizen citizen, Village village, ResourceKind kind, int amount,
        List<ResourceAmount> carried, List<ResourceAmount> lost)
    {
        var left = amount - village.Store(kind, amount);
        var held = Math.Min(left, Math.Max(0, StorageRules.TakeLimit - citizen.Carried(kind)));
        if (held > 0)
        {
            citizen.Carry(kind, held);
            carried.Add(new ResourceAmount(kind, held));
        }

        if (left - held > 0)
        {
            lost.Add(new ResourceAmount(kind, left - held));
        }
    }

    private static CycleReport? SettleJob(Citizen citizen, WorkContext context, DateTimeOffset now)
    {
        var job = BuildingCatalog.FindJob(citizen.CurrentTaskId!);
        if (job is null)
        {
            return null;
        }

        var village = context.Village;
        var plotId = citizen.CurrentSiteId!;
        var stage = citizen.CurrentSiteStage ?? 0;
        var site = village.Sites.SingleOrDefault(s => s.PlotId == plotId);
        var factor = YieldFactor(citizen, village, job.Primary, job.Secondary, now) / GameRules.StatFactorBase
                     * SkillRules.YieldMultiplier(citizen, job.Skill, 0)
                     * WeatherRules.JobMultiplier(village, WorkedAt(citizen, now));
        var amount = 0;
        var produced = new List<ResourceAmount>();
        var carried = new List<ResourceAmount>();
        var lost = new List<ResourceAmount>();

        if (site is not null && stage >= site.Stage && !site.IsFinished)
        {
            if (job.Id == BuildingCatalog.LendAHand)
            {
                var needed = ConstructionSite.CrewNeeded(site.Definition.Stages[stage], context.RecentCitizenCount);
                if (stage == site.Stage && site.HandCount(stage) < needed && !site.HasHand(stage, citizen.Id))
                {
                    site.AddHand(stage, citizen.Id);
                    amount = 1;
                }
            }
            else if (job.Kind == JobKind.Haul)
            {
                var resource = job.Resource!.Value;
                amount = Min(YieldMath.Whole(job.PerCycle * factor), Available(citizen, village, resource), site.Remaining(stage, job.Id));
                if (amount > 0)
                {
                    TakeFromHandsThenStorage(citizen, village, resource, amount);
                    site.Record(stage, job.Id, amount, citizen.Id);
                }
            }
            else if (stage == site.Stage)
            {
                amount = Min(YieldMath.Whole(job.PerCycle * factor), site.LaborRoom(stage, job.Id));
                if (amount > 0)
                {
                    site.Record(stage, job.Id, amount, citizen.Id);
                    if (job.Id == BuildingCatalog.FellTrees)
                    {
                        var wood = Forest.Fell(village, WorldMap.Footprint(plotId)!.Value,
                            amount * Forest.WoodPerTree / BuildingCatalog.FellWorkPerTree, now);
                        if (wood > 0)
                        {
                            produced.Add(new ResourceAmount(ResourceKind.Wood, wood));
                            Keep(citizen, village, ResourceKind.Wood, wood, carried, lost);
                        }
                    }
                }
            }
        }

        var overflowed = amount <= 0;
        var completed = site is null ? null : ConstructionRules.Advance(village, site, context.RecentCitizenCount, now);
        var hungry = Eat(citizen, village, now);

        var report = Finish(citizen, village, job.Id, job.Primary, job.Secondary, job.Skill, SkillRules.Challenge(citizen, job.Skill, 0),
            overflowed ? GameRules.OverflowExperienceMultiplier : 1, hungry,
            produced, carried, 0, overflowed ? 0 : amount, false, overflowed);
        return report with { SiteId = plotId, BuildingCompleted = completed, Lost = lost };
    }

    private static CycleReport Finish(
        Citizen citizen,
        Village village,
        string taskId,
        Stat primary,
        Stat secondary,
        Skill skill,
        double challenge,
        double experienceShare,
        bool hungry,
        IReadOnlyList<ResourceAmount> produced,
        IReadOnlyList<ResourceAmount> carried,
        double moraleGained,
        int contributed,
        bool rareFind,
        bool overflowed)
    {
        var earned = GameRules.BaseExperiencePerCycle
                     * challenge
                     * (citizen.CycleTired ? GameRules.TiredExperienceMultiplier : 1)
                     * experienceShare
                     * (hungry ? GameRules.HungryExperienceMultiplier : 1);
        var experience = YieldMath.Whole(earned);
        var secondaryExperience = YieldMath.Whole(earned * GameRules.SecondaryExperienceShare);

        var primaryResult = Progression.AddExperience(citizen.Stats, primary, experience);
        var secondaryResult = Progression.AddExperience(citizen.Stats, secondary, secondaryExperience);
        var skillLevelsGained = SkillRules.AddExperience(citizen, skill, experience);
        SkillRules.CatchUp(citizen);
        var renown = primaryResult.RenownGained + secondaryResult.RenownGained;
        citizen.Renown += renown;

        return new CycleReport(
            citizen.Id,
            citizen.Name,
            taskId,
            produced,
            carried,
            moraleGained,
            contributed,
            rareFind,
            primary,
            experience,
            secondary,
            secondaryExperience,
            skill,
            experience,
            skillLevelsGained,
            primaryResult.LevelsGained + secondaryResult.LevelsGained,
            renown,
            citizen.CycleTired,
            overflowed,
            Hungry: hungry);
    }

    /// <summary>
    /// Every <see cref="GameRules.CyclesPerMeal"/> finished cycles a citizen eats one food, from their hands first,
    /// then from storage. A hungry citizen tries again after every cycle. Returns true while the citizen is hungry.
    /// </summary>
    private static bool Eat(Citizen citizen, Village village, DateTimeOffset now)
    {
        citizen.CyclesSinceMeal++;
        if (citizen.CyclesSinceMeal < GameRules.CyclesPerMeal && !citizen.IsHungry)
        {
            return false;
        }

        var eaten = citizen.TakeFromLoad(ResourceKind.Food, 1);
        if (eaten == 0)
        {
            eaten = village.Take(ResourceKind.Food, 1);
        }

        var hungry = eaten == 0;
        if (!hungry)
        {
            citizen.CyclesSinceMeal = 0;
        }

        if (hungry != citizen.IsHungry)
        {
            citizen.FlushEnergy(now, village);
            citizen.IsHungry = hungry;
        }

        return hungry;
    }

    /// <summary>Stat factor with tiredness, morale and streak.</summary>
    private static double YieldFactor(Citizen citizen, Village village, Stat primary, Stat secondary, DateTimeOffset now) =>
        YieldMath.StatFactor(YieldMath.EffectiveStat(citizen.Stats.Get(primary), citizen.Stats.Get(secondary)))
        * (citizen.CycleTired ? GameRules.TiredYieldMultiplier : 1)
        * village.MoraleAt(now)
        * YieldMath.StreakFactor(citizen.Streak);

    private static void CheckReady(Citizen citizen, DateTimeOffset now)
    {
        if (citizen.Status != CitizenStatus.Active)
        {
            throw new GameRuleException("citizen.dormant", "Dormant citizens cannot work.");
        }

        if (CombatRules.IsInjured(citizen, now))
        {
            throw new GameRuleException("citizen.injured", "You are wounded and cannot work until the wound heals. A remedy heals it at once.",
                new Dictionary<string, object> { ["readyAt"] = citizen.InjuredUntil!.Value });
        }

        if (citizen.DungeonRunId is not null)
        {
            throw new GameRuleException("dungeon.busy", "You are away in the dungeon.");
        }

        if (citizen.CycleEndsAt is not null && now < citizen.CycleEndsAt)
        {
            throw new GameRuleException("task.cycleRunning", "The current task is not finished yet.");
        }

        if (!citizen.CycleSettled)
        {
            throw new InvalidOperationException("The previous cycle must be settled before starting a new one.");
        }
    }

    /// <summary>The weather of a cycle is the weather it started in.</summary>
    private static DateTimeOffset WorkedAt(Citizen citizen, DateTimeOffset now) => citizen.CycleStartedAt ?? now;

    private static void Begin(Citizen citizen, string taskId, TimeSpan duration, double energyCost, Village village, DateTimeOffset now)
    {
        var renewing = citizen.GraceEndsAt is not null && now <= citizen.GraceEndsAt;
        citizen.Streak = renewing ? citizen.Streak + 1 : 0;

        var energy = citizen.CurrentEnergy(now, village);
        citizen.CycleTired = energy < energyCost;
        citizen.StoredEnergy = citizen.CycleTired ? energy : energy - energyCost;
        citizen.EnergyUpdatedAt = now;

        citizen.CurrentTaskId = taskId;
        citizen.CurrentTechnologyId = null;
        citizen.Spot = null;
        citizen.IdleSpot = null;
        citizen.CycleStartedAt = now;
        citizen.CycleEndsAt = now + duration;
        citizen.CycleSettled = false;
    }

    /// <summary>The place on the map a task is done at, unless it is done at the camp, in a building or all over the land.</summary>
    private static TilePoint? NaturalPlace(TaskDefinition task) =>
        task.Workplace != WorldMap.Camp && WorldMap.Places.TryGetValue(task.Workplace, out var place) ? place : null;

    /// <summary>Walking time of a task: from where the work happens to where its result is dropped.</summary>
    public static TimeSpan Walk(Citizen citizen, Village village, TaskDefinition task) =>
        Walk(citizen, village, task, Workplace(village, task));

    /// <summary>Where the task is done: the yard of the building it needs once one stands, else its place on the map.</summary>
    public static TilePoint Workplace(Village village, TaskDefinition task) =>
        task.RequiredBuilding is { } kind && village.Structures.FirstOrDefault(s => s.Kind == kind) is { } building
            ? WorldMap.Yard(building.PlotId)
            : WorldMap.Place(task.Workplace);

    /// <summary>
    /// A camp tree the player picked is walked to and from as it is. Other spots cannot be checked,
    /// so they may make the walk longer than from the workplace, never shorter.
    /// </summary>
    private static TimeSpan Walk(Citizen citizen, Village village, TaskDefinition task, TilePoint? spot, DateTimeOffset now)
    {
        if (FarmRules.Walk(village, task, now) is { } farmWalk)
        {
            return farmWalk;
        }

        var standard = Walk(citizen, village, task);
        if (spot is not { } picked)
        {
            return standard;
        }

        var fromSpot = Walk(citizen, village, task, picked);
        return WorkSpots.TreeAt(village, task, picked, now) is not null || Wilderness.IsWild(task) || fromSpot > standard ? fromSpot : standard;
    }

    private static TimeSpan Walk(Citizen citizen, Village village, TaskDefinition task, TilePoint place)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        return task.Effect switch
        {
            TaskEffect.Resources when task.Outputs.Count > 0 =>
                WorldMap.Walk(place, village.DropPoint(task.Outputs[0].Resource, place)),
            TaskEffect.Expedition => WorldMap.Walk(place, camp),
            TaskEffect.Unload when citizen.Load.Count > 0 => WorldMap.Walk(camp, village.DropPoint(citizen.Load[0].Kind, camp)),
            _ => TimeSpan.Zero,
        };
    }

    private static void CheckProjectTask(Citizen citizen, TaskDefinition task, WorkContext context)
    {
        if (task.Effect is not (TaskEffect.Deliver or TaskEffect.Survey or TaskEffect.Build or TaskEffect.Supervise))
        {
            return;
        }

        var project = context.Project is { Status: ProjectStatus.Active } active
            ? active
            : throw new GameRuleException("project.none", "There is no construction project right now.");

        switch (task.Effect)
        {
            case TaskEffect.Deliver:
            {
                var resource = task.Outputs[0].Resource;
                var key = resource.ToString();
                if (project.Required(key) <= 0)
                {
                    throw new GameRuleException("project.materialNotNeeded", $"The project does not need {ResourceNames.Of(resource)}.", Resource(resource));
                }

                if (project.Remaining(key) == 0)
                {
                    throw new GameRuleException("project.materialComplete", $"Enough {ResourceNames.Of(resource)} has already been delivered.", Resource(resource));
                }

                RequireRoom(project, citizen, key, context);
                if (Available(citizen, context.Village, resource) < 1)
                {
                    throw new GameRuleException("project.storageEmpty", $"There is no {ResourceNames.Of(resource)} in storage. Gather some first.", Resource(resource));
                }

                break;
            }

            case TaskEffect.Survey:
                if (project.SurveyDone)
                {
                    throw new GameRuleException("project.surveyDone", "The survey is already finished.");
                }

                RequireRoom(project, citizen, VillageProject.SurveyKey, context);
                break;

            case TaskEffect.Build:
                if (!project.SurveyDone)
                {
                    throw new GameRuleException("project.surveyFirst", "An engineer must survey the site first.");
                }

                if (project.BuildDone)
                {
                    throw new GameRuleException("project.buildDone", "Construction is already finished.");
                }

                if (project.BuildProgress >= project.BuildLimit)
                {
                    throw new GameRuleException("project.needMaterials", "Not enough materials: deliver them to the site.");
                }

                if (project.Definition.NeedsEngineer && context.BuildersWorking >= context.SupervisionCapacity)
                {
                    throw context.SupervisionCapacity == 0
                        ? new GameRuleException("project.needSupervisor", "An engineer must supervise the construction.")
                        : new GameRuleException("project.supervisionFull", "All supervised builder places are taken. More engineers are needed.");
                }

                RequireRoom(project, citizen, VillageProject.BuildKey, context);
                break;

            case TaskEffect.Supervise:
                if (!project.Definition.NeedsEngineer)
                {
                    throw new GameRuleException("project.noSupervision", "This work is small enough to build without an engineer.");
                }

                if (!project.SurveyDone)
                {
                    throw new GameRuleException("project.surveyFirst", "An engineer must survey the site first.");
                }

                if (project.BuildDone)
                {
                    throw new GameRuleException("project.buildDone", "Construction is already finished.");
                }

                break;
        }
    }

    private static void RequireRoom(VillageProject project, Citizen citizen, string key, WorkContext context)
    {
        if (project.ContributionRoom(citizen.Id, key, context.ActiveCitizenCount) == 0)
        {
            throw new GameRuleException("project.shareTaken", "You already did your share of this part of the project. Let others work.");
        }
    }

    private static Dictionary<string, object> Resource(ResourceKind kind) => new() { ["resource"] = kind.ToString() };

    private static void CheckPrey(Village village, TaskDefinition task, DateTimeOffset now)
    {
        if (task.Hunt is not { } hunt || village.HerdOf(hunt.Prey).Current(now) >= WildlifeCatalog.MinimumToHunt)
        {
            return;
        }

        throw new GameRuleException("hunt.noAnimals",
            $"No {WildlifeCatalog.Get(hunt.Prey).Name} are left near the camp. Give them time to come back.",
            new Dictionary<string, object> { ["animal"] = hunt.Prey.ToString() });
    }

    /// <summary>
    /// A citizen whose hands and storage are both full of every good a task produces cannot start it.
    /// Expedition and roaming loot is random, so they need empty hands. Carrying to storage needs a load and room.
    /// </summary>
    private static void CheckHands(Citizen citizen, Village village, TaskDefinition task, DateTimeOffset now)
    {
        if (task.Effect == TaskEffect.Unload)
        {
            if (citizen.Load.Count == 0)
            {
                throw new GameRuleException("hands.empty", "Your hands are empty.");
            }

            if (citizen.Load.All(g => village.FreeSpace(g.Kind) == 0))
            {
                throw new GameRuleException("hands.noRoom", "Storage has no room for what you carry. Ask the Tsar or a Governor to plan more storage.");
            }

            return;
        }

        if (task.Effect == TaskEffect.Expedition && citizen.Load.Count > 0)
        {
            throw new GameRuleException("hands.fullExpedition", "Your hands are full. Unload or drop your load before an expedition.");
        }

        if (task.Effect == TaskEffect.Roam && citizen.Load.Count > 0)
        {
            throw new GameRuleException("hands.fullRoam", "Your hands are full. Unload or drop your load before roaming.");
        }

        var outputs = task.Effect switch
        {
            TaskEffect.Resources => task.Outputs,
            TaskEffect.Harvest when FarmRules.RipeBed(village, now) is { Crop: { } crop } => FarmCatalog.Get(crop).Harvest,
            _ => [],
        };
        bool NoRoom(ResourceAmount output) =>
            citizen.Carried(output.Resource) >= StorageRules.TakeLimit && village.FreeSpace(output.Resource) <= 0;
        var blocked = outputs.FirstOrDefault(NoRoom);
        if (blocked is not null && outputs.All(NoRoom))
        {
            throw new GameRuleException("hands.full",
                $"Your hands are full of {ResourceNames.Of(blocked.Resource)} and storage has no room for it. Carry it to storage or pick another job.",
                Resource(blocked.Resource));
        }
    }

    private static int Available(Citizen citizen, Village village, ResourceKind kind) =>
        citizen.Carried(kind) + village.Stock(kind);

    private static void TakeFromHandsThenStorage(Citizen citizen, Village village, ResourceKind kind, int amount)
    {
        var fromHands = citizen.TakeFromLoad(kind, amount);
        village.Take(kind, amount - fromHands);
    }

    private static int Min(params int[] values) => values.Min();
}
