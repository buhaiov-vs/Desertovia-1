using Desertod.Domain.Catalog;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;

namespace Desertod.Domain.Dungeons;

public enum HeroClass
{
    Brawler,
    Spearman,
    Swordsman,
    Archer,
    Shieldbearer,
    Healer,

    /// <summary>The Tsar's own figure; never offered to citizens.</summary>
    Voivode,
}

/// <summary>How a hero fights in a dungeon, and what the class needs from storage and from the citizen.</summary>
/// <param name="Weapon">Taken from storage for the run; the class is offered only while one is there.</param>
/// <param name="Armor">Taken from storage for the run instead of the best armor available.</param>
/// <param name="Skill">Craft the class needs, at <paramref name="SkillLevel"/>.</param>
/// <param name="Stat">Stat the dungeon experience trains.</param>
/// <param name="Range">Reach of an attack, in tiles.</param>
/// <param name="HealthMultiplier">Applied to the health from Endurance.</param>
/// <param name="DamageMultiplier">Applied to the fight power.</param>
/// <param name="Heal">Health given back to an ally per use; 0 when the class cannot heal.</param>
public sealed record HeroClassDefinition(
    HeroClass Class,
    ResourceKind? Weapon,
    ResourceKind? Armor,
    Skill? Skill,
    int SkillLevel,
    Stat Stat,
    double Range,
    double AttackSeconds,
    double HealthMultiplier,
    double DamageMultiplier,
    double Heal = 0);

public static class HeroClassCatalog
{
    /// <summary>Health of a hero before Endurance.</summary>
    public const double BaseHealth = 20;

    public const double HealthPerEndurance = 4;

    /// <summary>Arrows an archer takes, at most; one is shot per attack.</summary>
    public const int Quiver = 12;

    /// <summary>Remedies a healer takes as their kit; one is used up if they heal anyone.</summary>
    public const int HealerRemedies = 1;

    public const double VoivodeHealth = 80;
    public const double VoivodeDamage = 8;

    /// <summary>Tiles per second.</summary>
    public const double HeroSpeed = 3.2;

    public const double FleeSpeed = 4;

    public static IReadOnlyList<HeroClassDefinition> Classes { get; } =
    [
        new(HeroClass.Brawler, null, null, null, 0, Stat.Strength, 0.9, 1.0, 1, 1),
        new(HeroClass.Spearman, ResourceKind.Spear, null, null, 0, Stat.Strength, 1.5, 1.1, 1, 1),
        new(HeroClass.Swordsman, ResourceKind.Sword, null, Skill.Guarding, 2, Stat.Strength, 1.0, 1.0, 1, 1.1),
        new(HeroClass.Archer, ResourceKind.Bow, null, Skill.Hunting, 2, Stat.Dexterity, 5, 1.4, 0.9, 1),
        new(HeroClass.Shieldbearer, null, ResourceKind.Shield, Skill.Guarding, 1, Stat.Endurance, 0.9, 1.2, 1.5, 0.7),
        new(HeroClass.Healer, null, null, Skill.Scholarship, 2, Stat.Intelligence, 3, 1.5, 1, 0.6, Heal: 8),
        new(HeroClass.Voivode, null, null, null, 0, Stat.Strength, 1.0, 1.0, 1, 1),
    ];

    public static HeroClassDefinition Get(HeroClass heroClass) => Classes.Single(c => c.Class == heroClass);

    public static double Health(int endurance, HeroClassDefinition definition) =>
        (BaseHealth + HealthPerEndurance * endurance) * definition.HealthMultiplier;
}
