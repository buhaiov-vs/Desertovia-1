using Desertod.Domain.Catalog;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;

namespace Desertod.Domain.Skills;

/// <param name="Level">Skill level these stats are needed for.</param>
public sealed record SkillLevelRequirement(int Level, IReadOnlyList<StatRequirement> Stats);

/// <summary>
/// A craft and the stats it rests on. Stats are what a citizen is; a craft is what they do with it,
/// so a level in a craft can only be reached with enough of its stats.
/// </summary>
/// <param name="Primary">Level L needs this stat at L − 1.</param>
/// <param name="Secondary">Level L needs this stat at (L − 1) / 2, rounded down.</param>
public sealed record SkillDefinition(Skill Kind, Stat Primary, Stat Secondary)
{
    public IReadOnlyList<StatRequirement> RequirementsFor(int level) =>
    [
        new(Primary, Math.Max(0, level - 1)),
        new(Secondary, Math.Max(0, (level - 1) / 2)),
    ];

    /// <summary>Stats needed for every level, from 1 to the maximum.</summary>
    public IReadOnlyList<SkillLevelRequirement> Levels =>
        Enumerable.Range(1, GameRules.SkillMaximum).Select(level => new SkillLevelRequirement(level, RequirementsFor(level))).ToList();

    public bool Allows(StatBlock stats, int level) => RequirementsFor(level).All(r => stats.Get(r.Stat) >= r.Minimum);
}

public static class SkillCatalog
{
    public static IReadOnlyList<SkillDefinition> Skills { get; } =
    [
        new(Skill.Woodcutting, Stat.Strength, Stat.Endurance),
        new(Skill.Quarrying, Stat.Strength, Stat.Endurance),
        new(Skill.Mining, Stat.Strength, Stat.Endurance),
        new(Skill.Smithing, Stat.Strength, Stat.Dexterity),
        new(Skill.Gathering, Stat.Dexterity, Stat.Intelligence),
        new(Skill.Hunting, Stat.Dexterity, Stat.Endurance),
        new(Skill.Fishing, Stat.Dexterity, Stat.Endurance),
        new(Skill.Exploring, Stat.Dexterity, Stat.Endurance),
        new(Skill.Baking, Stat.Dexterity, Stat.Endurance),
        new(Skill.Carpentry, Stat.Dexterity, Stat.Strength),
        new(Skill.Weaponcraft, Stat.Dexterity, Stat.Strength),
        new(Skill.Farming, Stat.Endurance, Stat.Dexterity),
        new(Skill.Hauling, Stat.Endurance, Stat.Strength),
        new(Skill.Guarding, Stat.Endurance, Stat.Strength),
        new(Skill.Building, Stat.Intelligence, Stat.Strength),
        new(Skill.Scholarship, Stat.Intelligence, Stat.Charisma),
        new(Skill.Weatherlore, Stat.Intelligence, Stat.Endurance),
        new(Skill.Entertaining, Stat.Charisma, Stat.Dexterity),
        new(Skill.Trading, Stat.Charisma, Stat.Intelligence),
    ];

    private static readonly Dictionary<Skill, SkillDefinition> ByKind = Skills.ToDictionary(s => s.Kind);

    public static SkillDefinition Get(Skill skill) => ByKind[skill];
}
