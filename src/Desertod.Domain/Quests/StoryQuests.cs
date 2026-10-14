using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;

namespace Desertod.Domain.Quests;

/// <summary>
/// The fixed chain of village goals, from the first fire to a bell for the monastery (docs/story-quests.md).
/// One story quest is open at a time; the next one opens when it is completed.
/// </summary>
public static class StoryQuests
{
    public const string TendFire = "tend-fire";
    public const int FireCycles = 5;
    public const int StoresFood = 20;

    private static readonly string[] ShelterGathering = ["chop-wood", "cut-reeds"];

    public static IReadOnlyList<QuestKind> Chain { get; } =
    [
        QuestKind.Fire,
        QuestKind.Shelter,
        QuestKind.Stores,
        QuestKind.FirstHouse,
        QuestKind.Monument,
        QuestKind.FishingSpot,
        QuestKind.Palisade,
        QuestKind.TsarBridge,
        QuestKind.MonksGold,
        QuestKind.MonasteryBell,
        QuestKind.GreatGranary,
    ];

    public const string Fish = "fish";
    public const string Supervise = "supervise";
    public const int FishCatches = 5;
    public const int GoldDigs = 20;

    public static bool IsStory(QuestKind kind) => Chain.Contains(kind);

    /// <summary>The project a story quest starts when it opens.</summary>
    public static string? ProjectOf(QuestKind kind) => kind switch
    {
        QuestKind.Monument => ProjectCatalog.TsarMonument,
        QuestKind.FishingSpot => ProjectCatalog.FishingPier,
        QuestKind.Palisade => ProjectCatalog.Palisade,
        QuestKind.TsarBridge => ProjectCatalog.TsarBridge,
        QuestKind.MonasteryBell => ProjectCatalog.BellTower,
        QuestKind.GreatGranary => ProjectCatalog.GreatGranary,
        _ => null,
    };

    /// <summary>
    /// The project to start for the open story quest: none while any project runs, or once it is finished.
    /// </summary>
    public static ProjectDefinition? ProjectToStart(
        IEnumerable<VillageQuest> active,
        VillageProject? running,
        IReadOnlyCollection<string> completedProjects)
    {
        var id = active.Where(q => q.Status == QuestStatus.Active).Select(q => ProjectOf(q.Kind)).FirstOrDefault(p => p is not null);
        if (id is null || running is not null || completedProjects.Contains(id))
        {
            return null;
        }

        return ProjectCatalog.Find(id);
    }

    /// <summary>The next story quest to open, or none while one is open or once the chain is finished.</summary>
    public static VillageQuest? Open(
        Village village,
        IReadOnlyCollection<VillageQuest> active,
        IReadOnlyCollection<QuestKind> completed,
        DateTimeOffset now)
    {
        if (active.Any(q => IsStory(q.Kind)))
        {
            return null;
        }

        var next = Chain.Where(kind => !completed.Contains(kind)).Select(kind => (QuestKind?)kind).FirstOrDefault();
        return next is { } kind ? VillageQuest.Open(village.Id, kind, null, Target(kind), now) : null;
    }

    public static int Target(QuestKind kind) => kind switch
    {
        QuestKind.Fire => FireCycles,
        QuestKind.Stores => StoresFood,
        QuestKind.FishingSpot => FishCatches,
        QuestKind.MonksGold => GoldDigs,
        _ => 0,
    };

    public static IReadOnlyList<QuestObjective> Objectives(
        VillageQuest quest,
        Village village,
        IReadOnlyCollection<string> completedProjects) => quest.Kind switch
    {
        QuestKind.Monument or QuestKind.Palisade or QuestKind.TsarBridge or QuestKind.MonasteryBell or QuestKind.GreatGranary =>
        [
            Project(quest.Kind, completedProjects),
        ],
        QuestKind.FishingSpot =>
        [
            Project(quest.Kind, completedProjects),
            Counted(Fish, quest),
        ],
        QuestKind.Fire =>
        [
            Counted(TendFire, quest),
        ],
        QuestKind.MonksGold =>
        [
            Counted(GoldVein.DigTask, quest),
        ],
        QuestKind.Shelter => Building("lean-to", BuildingKind.LeanTo, village),
        QuestKind.Stores =>
        [
            new("store-food", QuestAudience.Citizens, village.Stock(ResourceKind.Food) >= quest.Target,
                village.Stock(ResourceKind.Food), quest.Target),
        ],
        QuestKind.FirstHouse => Building("hut", BuildingKind.Hut, village),
        _ => throw new ArgumentOutOfRangeException(nameof(quest), quest.Kind, null),
    };

    /// <summary>Whether a finished cycle counts for the quest; counted objectives count these cycles.</summary>
    public static bool Helps(VillageQuest quest, Village village, CycleReport report)
    {
        var building = QuestRules.BuildingWorkedOn(village, report);
        return quest.Kind switch
        {
            QuestKind.Fire => report.TaskId == TendFire,
            QuestKind.Shelter => building == BuildingKind.LeanTo || ShelterGathering.Contains(report.TaskId),
            QuestKind.Stores => QuestRules.Stored(report, ResourceKind.Food) > 0 || building == BuildingKind.DryingRack,
            QuestKind.FirstHouse => building == BuildingKind.Hut,
            QuestKind.Monument or QuestKind.Palisade => ProjectWork(report),
            QuestKind.TsarBridge or QuestKind.MonasteryBell or QuestKind.GreatGranary =>
                ProjectWork(report) || (report.TaskId == Supervise && !report.Overflowed),
            // Counted objective: only catches count; pier work is rewarded by the project itself.
            QuestKind.FishingSpot => report.TaskId == Fish,
            QuestKind.MonksGold => report.TaskId == GoldVein.DigTask && !report.Overflowed,
            _ => false,
        };
    }

    private static bool ProjectWork(CycleReport report) => report.ProjectContribution > 0 && report.SiteId is null;

    public static bool Built(Village village, BuildingKind kind) =>
        village.CountOf(kind) > village.Sites.Count(s => s.Kind == kind);

    private static QuestObjective Project(QuestKind kind, IReadOnlyCollection<string> completedProjects)
    {
        var id = ProjectOf(kind)!;
        return new($"project-{id}", QuestAudience.Citizens, completedProjects.Contains(id));
    }

    private static QuestObjective Counted(string id, VillageQuest quest)
    {
        var counted = quest.Contributions.Sum(c => c.Amount);
        return new(id, QuestAudience.Citizens, counted >= quest.Target, counted, quest.Target);
    }

    /// <summary>The Tsar or a Governor plans the building, citizens build it.</summary>
    private static QuestObjective[] Building(string id, BuildingKind kind, Village village)
    {
        var built = Built(village, kind);
        return
        [
            new($"plan-{id}", QuestAudience.Rulers, built || village.Sites.Any(s => s.Kind == kind)),
            new($"build-{id}", QuestAudience.Citizens, built),
        ];
    }
}
