using Desertod.Domain.Catalog;

namespace Desertod.Domain.Combat;

public enum RaidSource
{
    /// <summary>Hungry beasts come out of the woods at night.</summary>
    Night,

    /// <summary>The Tsar called the dead up for the viewers.</summary>
    Undead,
}

public enum RaidOutcome
{
    Pending,
    Repelled,
    Lost,
}

/// <summary>How many of the undead the Tsar calls: more of them, in more waves.</summary>
public enum RaidSize
{
    Small,
    Medium,
    Large,
}

/// <summary>An attack on the camp: on the way until <see cref="StrikesAt"/>, then fought and kept for the record.</summary>
public sealed class Raid
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public RaidSource Source { get; set; }
    public EnemyKind Enemy { get; set; }
    public int Count { get; set; }

    /// <summary>Undead come in waves; a night raid is a single wave.</summary>
    public int Wave { get; set; } = 1;

    public int Waves { get; set; } = 1;

    /// <summary>From then on the players see the attack coming.</summary>
    public DateTimeOffset AnnouncedAt { get; set; }

    public DateTimeOffset StrikesAt { get; set; }
    public RaidOutcome Outcome { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public int Killed { get; set; }
    public int Defenders { get; set; }
    public int Injured { get; set; }
    public int DamagedBuildings { get; set; }
    public List<RaidGood> Stolen { get; set; } = [];
    public List<RaidGood> Loot { get; set; } = [];

    public bool IsPending => Outcome == RaidOutcome.Pending;
}

public sealed class RaidGood
{
    public ResourceKind Kind { get; set; }
    public int Amount { get; set; }
}
