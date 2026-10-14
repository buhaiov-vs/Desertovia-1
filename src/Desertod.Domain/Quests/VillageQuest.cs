using Desertod.Domain.Catalog;

namespace Desertod.Domain.Quests;

/// <summary>A need of the tribe. Quests open by themselves when the village has the problem.</summary>
public enum QuestKind
{
    /// <summary>Someone finished a cycle with no food in storage.</summary>
    Hunger,

    /// <summary>More citizens than beds: someone sleeps under the trees.</summary>
    Cold,

    /// <summary>Gathered goods of one kind did not fit in storage.</summary>
    Storage,

    // Story quests: a fixed chain of village goals, see StoryQuests.Chain.
    Fire,
    Shelter,
    Stores,
    FirstHouse,
    Monument,
    FishingSpot,
    Palisade,
    TsarBridge,
    MonksGold,
    MonasteryBell,
    GreatGranary,
}

public enum QuestStatus
{
    Active,
    Completed,
}

/// <summary>Who an objective is for: the rulers (the Tsar and the Governors) plan, citizens do the work.</summary>
public enum QuestAudience
{
    Rulers,
    Citizens,
}

public sealed class VillageQuest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public QuestKind Kind { get; set; }

    /// <summary>The good a <see cref="QuestKind.Storage"/> quest is about; null for other kinds.</summary>
    public ResourceKind? Resource { get; set; }

    /// <summary>Kind and resource; at most one active quest per key and village.</summary>
    public required string Key { get; set; }

    public QuestStatus Status { get; set; } = QuestStatus.Active;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Food to store for hunger, free room for storage; fixed when the quest opens. Unused for cold.</summary>
    public int Target { get; set; }

    public List<QuestContribution> Contributions { get; set; } = [];

    public static string KeyFor(QuestKind kind, ResourceKind? resource) =>
        resource is null ? kind.ToString() : $"{kind}:{resource}";

    public static VillageQuest Open(Guid villageId, QuestKind kind, ResourceKind? resource, int target, DateTimeOffset now) => new()
    {
        VillageId = villageId,
        Kind = kind,
        Resource = resource,
        Key = KeyFor(kind, resource),
        Target = target,
        StartedAt = now,
    };

    public void Record(Guid citizenId, int amount)
    {
        var contribution = Contributions.SingleOrDefault(c => c.CitizenId == citizenId);
        if (contribution is null)
        {
            contribution = new QuestContribution { VillageQuestId = Id, CitizenId = citizenId };
            Contributions.Add(contribution);
        }

        contribution.Amount += amount;
    }
}

/// <summary>Useful cycles a citizen did while the quest was open; the reward is split by these.</summary>
public sealed class QuestContribution
{
    public Guid VillageQuestId { get; set; }
    public Guid CitizenId { get; set; }
    public int Amount { get; set; }
}
