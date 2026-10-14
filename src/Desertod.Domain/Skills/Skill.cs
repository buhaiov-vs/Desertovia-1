namespace Desertod.Domain.Skills;

/// <summary>
/// Crafts a citizen gets better at by doing them. Every task, construction job and research trains one craft,
/// besides the stats it trains.
/// </summary>
public enum Skill
{
    Woodcutting,
    Quarrying,
    Gathering,
    Hunting,
    Fishing,
    Farming,
    Mining,
    Smithing,
    Carpentry,
    Baking,
    Scholarship,
    Building,
    Hauling,
    Entertaining,
    Exploring,
    Trading,
    Guarding,
    Weatherlore,
    Weaponcraft,
}

public sealed class SkillProgress
{
    public Skill Kind { get; set; }
    public int Level { get; set; }

    /// <summary>Experience towards the next level.</summary>
    public int Experience { get; set; }
}
