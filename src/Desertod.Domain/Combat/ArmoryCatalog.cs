using Desertod.Domain.Catalog;

namespace Desertod.Domain.Combat;

/// <param name="Power">Added to the fight power of the citizen holding it.</param>
/// <param name="BreakChance">Chance to break in each fight.</param>
/// <param name="Arrows">Arrows shot in one fight; the weapon is useless without them.</param>
public sealed record WeaponDefinition(ResourceKind Kind, double Power, double BreakChance, int Arrows = 0);

/// <param name="Protection">Chance that a blow that would wound the wearer is stopped.</param>
/// <param name="BreakChance">Chance to break in each fight.</param>
public sealed record ArmorDefinition(ResourceKind Kind, double Protection, double BreakChance);

/// <summary>
/// Weapons and armor are goods kept in village storage. A citizen setting off on a dangerous task or a watch takes
/// the best ones there are and puts them back when the task ends.
/// </summary>
public static class ArmoryCatalog
{
    /// <summary>Arrows a citizen takes with a bow: enough for a few fights.</summary>
    public const int QuiverSize = 6;

    /// <summary>Best first: a citizen takes the first one available.</summary>
    public static IReadOnlyList<WeaponDefinition> Weapons { get; } =
    [
        new(ResourceKind.Sword, 6, 0.03),
        new(ResourceKind.Bow, 4, 0.05, Arrows: 2),
        new(ResourceKind.Spear, 3, 0.1),
    ];

    /// <summary>Best first.</summary>
    public static IReadOnlyList<ArmorDefinition> Armors { get; } =
    [
        new(ResourceKind.Shield, 0.4, 0.05),
        new(ResourceKind.LeatherArmor, 0.3, 0.08),
    ];

    public static WeaponDefinition? Weapon(ResourceKind? kind) => Weapons.FirstOrDefault(w => w.Kind == kind);

    public static ArmorDefinition? Armor(ResourceKind? kind) => Armors.FirstOrDefault(a => a.Kind == kind);
}
