using Desertod.Domain.Catalog;
using Desertod.Domain.Dungeons;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

/// <summary>How one protector of a den fights. A den of a pack splits the health and the damage of its level among its protectors.</summary>
/// <param name="Damage">One blow, before the hero's armor.</param>
/// <param name="Speed">Tiles per second; 0 for a protector that does not walk.</param>
/// <param name="AttackRange">Reach of a blow or a shot, in tiles.</param>
/// <param name="Experience">Experience the hero gets for slaying it.</param>
/// <param name="InjuryMinutes">How long a hero it fells cannot work.</param>
public sealed record DenGuard(
    int Level,
    double Health,
    double Damage,
    double AttackSeconds,
    double Speed,
    double AttackRange,
    int Experience,
    int InjuryMinutes,
    IReadOnlyList<DungeonLoot> Loot);

/// <summary>
/// Protector numbers of the thirty den levels. One protector has its share of the level's pack (<see cref="SpawnLevel.Protectors"/>):
/// alone, a brand-new citizen with bare hands beats a pack of level 1 slimes, and a citizen with every stat at 10, a sword
/// and a shield beats the one level 30 lich. A den holds <see cref="SpawnRings.ProtectorsPerDen"/> of them, so a whole den
/// takes several citizens. Between levels the pack's health grows 10% a level and its damage 2.8%.
/// </summary>
public static class DenGuards
{
    public const double FirstDenHealth = 54;
    public const double HealthGrowth = 1.10;

    /// <summary>Damage a second of the whole level 1 den.</summary>
    public const double FirstDenDamage = 1.05;
    public const double DamageGrowth = 1.028;

    /// <summary>A hero this close to the den, in tiles, wakes all of its protectors.</summary>
    public const double AggroRange = 6;

    /// <summary>A slain protector stands at the den again after this long.</summary>
    public static readonly TimeSpan Respawn = TimeSpan.FromMinutes(30);

    /// <summary>Chasing pace, in tiles a second: faster than a walking citizen, so a hero who walks on is caught before the leash.</summary>
    public const double PackSpeed = 2.2;
    public const double SingleSpeed = 2.0;

    /// <summary>Protectors going home walk this much faster than they chase.</summary>
    public const double ReturnSpeedMultiplier = 1.5;

    public const double MeleeRange = 1;

    /// <summary>Levels whose protector stays rooted: the poison plant and the lich on his throne.</summary>
    private static readonly IReadOnlySet<int> Rooted = new HashSet<int> { 14, 30 };

    /// <summary>Protectors that strike from afar, by level, in tiles.</summary>
    private static readonly IReadOnlyDictionary<int, double> Ranged = new Dictionary<int, double>
    {
        [13] = 4, // wraith
        [14] = 3, // poison plant
        [21] = 4, // orc shaman
        [25] = 4, // fire spirit
        [30] = 5, // lich
    };

    public static double DenHealth(int level) => FirstDenHealth * Math.Pow(HealthGrowth, level - 1);

    public static double DenDamagePerSecond(int level) => FirstDenDamage * Math.Pow(DamageGrowth, level - 1);

    public static DenGuard For(SpawnLevel level)
    {
        var count = Math.Max(1, level.Protectors);
        var attackSeconds = count == 1 ? 2.0 : 1.4;
        return new DenGuard(
            level.Level,
            Math.Round(DenHealth(level.Level) / count),
            Math.Round(DenDamagePerSecond(level.Level) / count * attackSeconds, 1),
            attackSeconds,
            Rooted.Contains(level.Level) ? 0 : count == 1 ? SingleSpeed : PackSpeed,
            Ranged.GetValueOrDefault(level.Level, MeleeRange),
            (int)Math.Ceiling((8 + 4.0 * level.Level) / count),
            20 + 2 * (level.Level - 1),
            Loot(level.Level));
    }

    private static IReadOnlyList<DungeonLoot> Loot(int level) => level switch
    {
        < 10 => [new(ResourceKind.Food, 1, 2, 0.5), new(ResourceKind.Leather, 1, 1, 0.25)],
        < 20 => [new(ResourceKind.IronOre, 1, 2, 0.5), new(ResourceKind.Coal, 1, 2, 0.3), new(ResourceKind.Leather, 1, 1, 0.25)],
        < 30 => [new(ResourceKind.IronOre, 1, 3, 0.5), new(ResourceKind.Gold, 1, 1, 0.3), new(ResourceKind.Remedy, 1, 1, 0.15)],
        _ => [new(ResourceKind.Gold, 2, 4, 1), new(ResourceKind.Remedy, 1, 1, 0.5)],
    };

    /// <summary>Protectors in one row in front of the den; more stand in a second, wider row.</summary>
    public const int RowSize = 4;

    /// <summary>
    /// Where each protector stands at rest, in tiles: in rows of <see cref="RowSize"/> on arcs in front of the den,
    /// the outer row wider and staggered, or aside for a lone one, on the camp side. A roaming foe stands on its own
    /// tile. The web client's <c>DenLayer</c> draws them at the same places.
    /// </summary>
    public static IReadOnlyList<TilePoint> Homes(SpawnPoint point, TilePoint camp)
    {
        if (point.Roams)
        {
            return [point.Center];
        }

        var count = point.Protectors;
        var center = point.Center;
        var facing = camp.X < center.X ? -1 : 1;
        var homes = new TilePoint[count];
        for (var index = 0; index < count; index++)
        {
            var (angle, radius) = count == 1
                ? (facing < 0 ? Math.PI * 0.75 : Math.PI * 0.25, 1.2)
                : Arc(index, count);
            homes[index] = new TilePoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius * 0.6);
        }

        return homes;
    }

    private static (double Angle, double Radius) Arc(int index, int count)
    {
        var row = index / RowSize;
        var inRow = Math.Min(RowSize, count - row * RowSize);
        var share = inRow == 1 ? 0.5 : (double)(index % RowSize) / (inRow - 1);
        var span = row == 0 ? (count > RowSize ? 0.5 : 0.7) : 0.8;
        return (Math.PI * (0.5 + (share - 0.5) * span), 1.3 + row);
    }
}
