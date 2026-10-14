using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;

namespace Desertod.Domain.Quests;

/// <param name="Current">Progress for citizens; the Tsar's view never shows numbers.</param>
public sealed record QuestObjective(string Id, QuestAudience Audience, bool Done, int? Current = null, int? Required = null);

/// <param name="RenownPool">Split between the helpers by their useful cycles.</param>
public sealed record QuestReward(int RenownPool, double Morale);

public sealed record QuestCompletion(Guid QuestId, QuestKind Kind, ResourceKind? Resource, IReadOnlyDictionary<Guid, int> RenownByCitizen);

/// <param name="HungryCitizens">Active citizens whose last cycle found no food.</param>
public sealed record VillageNeeds(int ActiveCitizens, int HungryCitizens);

/// <summary>
/// Needs of the tribe. A quest opens when the village has a problem and closes when the problem is solved:
/// the Tsar or a Governor plans the building, citizens build it and do the work around it.
/// </summary>
public static class QuestRules
{
    public const int FoodPerCitizen = 3;
    public const int FoodMinimum = 10;
    public const int FoodMaximum = 40;

    /// <summary>Free room a full storage quest asks for: one Heap slot.</summary>
    public const int StorageRoom = 10;

    public static IReadOnlyDictionary<QuestKind, QuestReward> Rewards { get; } = new Dictionary<QuestKind, QuestReward>
    {
        [QuestKind.Hunger] = new(15, 0.03),
        [QuestKind.Cold] = new(20, 0.03),
        [QuestKind.Storage] = new(10, 0.01),
        [QuestKind.Fire] = new(10, 0.02),
        [QuestKind.Shelter] = new(15, 0.03),
        [QuestKind.Stores] = new(15, 0.03),
        [QuestKind.FirstHouse] = new(25, 0.05),
        [QuestKind.Monument] = new(40, 0.08),
        [QuestKind.FishingSpot] = new(30, 0.04),
        [QuestKind.Palisade] = new(50, 0.08),
        [QuestKind.TsarBridge] = new(80, 0.1),
        [QuestKind.MonksGold] = new(50, 0.05),
        [QuestKind.MonasteryBell] = new(80, 0.15),
        [QuestKind.GreatGranary] = new(60, 0.05),
    };

    public static int FoodTarget(int activeCitizens) =>
        Math.Clamp(FoodPerCitizen * activeCitizens, FoodMinimum, FoodMaximum);

    public static int Beds(Village village) => village.Structures.Sum(s => BuildingCatalog.Get(s.Kind).Shelter);

    /// <summary>Opens a quest for every current problem that has no open quest yet.</summary>
    public static IReadOnlyList<VillageQuest> Open(
        Village village,
        IReadOnlyCollection<VillageQuest> active,
        VillageNeeds needs,
        CycleReport? report,
        DateTimeOffset now)
    {
        var candidates = new List<VillageQuest>();
        if (needs.HungryCitizens > 0 || report?.Hungry == true)
        {
            candidates.Add(VillageQuest.Open(village.Id, QuestKind.Hunger, null, FoodTarget(needs.ActiveCitizens), now));
        }

        if (Beds(village) < needs.ActiveCitizens)
        {
            candidates.Add(VillageQuest.Open(village.Id, QuestKind.Cold, null, 0, now));
        }

        foreach (var carried in report?.Carried ?? [])
        {
            candidates.Add(VillageQuest.Open(village.Id, QuestKind.Storage, carried.Resource, StorageRoom, now));
        }

        var keys = active.Select(q => q.Key).ToHashSet();
        return candidates.Where(q => keys.Add(q.Key) && !IsComplete(q, village, needs.ActiveCitizens)).ToList();
    }

    /// <summary>Credits the citizen whose cycle just finished on every open quest that the cycle helped.</summary>
    public static void Credit(Village village, IEnumerable<VillageQuest> active, CycleReport report)
    {
        foreach (var quest in active.Where(q => q.Status == QuestStatus.Active && Helps(q, village, report)))
        {
            quest.Record(report.CitizenId, 1);
        }
    }

    /// <param name="completedProjects">Finished project ids; story quests about projects read them.</param>
    public static IReadOnlyList<QuestObjective> Objectives(
        VillageQuest quest,
        Village village,
        int activeCitizens,
        IReadOnlyCollection<string>? completedProjects = null)
    {
        switch (quest.Kind)
        {
            case QuestKind.Hunger:
            {
                var built = village.Storages.Any(s => IsFoodStore(s.Kind));
                var planned = village.Sites.Any(s => IsFoodStore(s.Definition.Storage));
                var food = village.Stock(ResourceKind.Food);
                return
                [
                    new("plan-food-storage", QuestAudience.Rulers, built || planned),
                    new("build-food-storage", QuestAudience.Citizens, built),
                    new("stock-food", QuestAudience.Citizens, food >= quest.Target, food, quest.Target),
                ];
            }

            case QuestKind.Cold:
            {
                var beds = Beds(village);
                var planned = village.Sites.Sum(s => s.Definition.Shelter);
                return
                [
                    new("plan-shelter", QuestAudience.Rulers, beds + planned >= activeCitizens),
                    new("build-shelter", QuestAudience.Citizens, beds >= activeCitizens, beds, activeCitizens),
                ];
            }

            case QuestKind.Storage:
            {
                var resource = quest.Resource!.Value;
                var free = village.FreeSpace(resource);
                var planned = village.Sites.Any(s => Stores(s.Definition.Storage, resource));
                var room = free >= quest.Target;
                return
                [
                    new("plan-storage", QuestAudience.Rulers, room || planned),
                    new("make-room", QuestAudience.Citizens, room, free, quest.Target),
                ];
            }

            default:
                return StoryQuests.Objectives(quest, village, completedProjects ?? []);
        }
    }

    public static bool IsComplete(
        VillageQuest quest,
        Village village,
        int activeCitizens,
        IReadOnlyCollection<string>? completedProjects = null) =>
        Objectives(quest, village, activeCitizens, completedProjects).All(o => o.Done);

    /// <summary>Closes the quest, lifts morale and splits the Renown pool by useful cycles.</summary>
    public static QuestCompletion Complete(
        VillageQuest quest,
        Village village,
        IReadOnlyDictionary<Guid, Citizen> contributors,
        DateTimeOffset now)
    {
        var reward = Rewards[quest.Kind];
        quest.Status = QuestStatus.Completed;
        quest.CompletedAt = now;
        village.AddMorale(reward.Morale, now);

        var total = quest.Contributions.Sum(c => c.Amount);
        var renown = new Dictionary<Guid, int>();
        foreach (var contribution in quest.Contributions)
        {
            if (total <= 0 || !contributors.TryGetValue(contribution.CitizenId, out var citizen))
            {
                continue;
            }

            var amount = (int)Math.Round((double)reward.RenownPool * contribution.Amount / total, MidpointRounding.AwayFromZero);
            citizen.Renown += amount;
            renown[contribution.CitizenId] = amount;
        }

        return new QuestCompletion(quest.Id, quest.Kind, quest.Resource, renown);
    }

    /// <summary>The building whose site the cycle moved forward, if any.</summary>
    internal static BuildingKind? BuildingWorkedOn(Village village, CycleReport report) =>
        report.ProjectContribution > 0 && report.SiteId is { } plotId
            ? report.BuildingCompleted ?? village.KindAt(plotId)
            : null;

    private static bool Helps(VillageQuest quest, Village village, CycleReport report)
    {
        var definition = BuildingWorkedOn(village, report) is { } kind ? BuildingCatalog.Get(kind) : null;

        switch (quest.Kind)
        {
            case QuestKind.Hunger:
                return Stored(report, ResourceKind.Food) > 0 || IsFoodStore(definition?.Storage);

            case QuestKind.Cold:
                return definition is { Shelter: > 0 };

            case QuestKind.Storage:
            {
                var resource = quest.Resource!.Value;
                var unloaded = report.TaskId == GameCatalog.CarryToStorage && Stored(report, resource) > 0;
                var hauledAway = report.ProjectContribution > 0 && BuildingCatalog.FindJob(report.TaskId)?.Resource == resource;
                return unloaded || hauledAway || Stores(definition?.Storage, resource);
            }

            default:
                return StoryQuests.Helps(quest, village, report);
        }
    }

    /// <summary>What actually went into storage: produced minus what stayed in the citizen's hands or was lost.</summary>
    internal static int Stored(CycleReport report, ResourceKind kind) =>
        report.Produced.Where(p => p.Resource == kind).Sum(p => p.Amount)
        - report.Carried.Where(c => c.Resource == kind).Sum(c => c.Amount)
        - report.Lost.Where(l => l.Resource == kind).Sum(l => l.Amount);

    /// <summary>Storage made for food; the Heap takes food too but lets it rot among the sticks.</summary>
    private static bool IsFoodStore(StorageKind? kind) =>
        kind is { } storage && StorageCatalog.Get(storage) is { IsGeneral: false } definition && definition.AcceptsGood(ResourceKind.Food);

    private static bool Stores(StorageKind? kind, ResourceKind resource) =>
        kind is { } storage && StorageCatalog.Get(storage).AcceptsGood(resource);
}
