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

    /// <summary>A roaming foe this close, in tiles, wakes. A den calls its pack from <see cref="Sight"/>.</summary>
    public const double AggroRange = 6;

    /// <summary>
    /// Protectors who can walk circle the den at this distance, in tiles. Eight of them leave a gap of
    /// <c>PatrolRadius * sin(π/8)</c> on the way in, and that gap stays inside <see cref="Sight"/>, so the
    /// spots around the den, two tiles out, cannot be reached without meeting one. A protector that cannot
    /// walk stands closer, at its own reach, and still covers that middle.
    /// </summary>
    public const double PatrolRadius = 7;

    /// <summary>A protector sees a hero this close, in tiles, and calls the rest of the den.</summary>
    public const double Sight = 4;

    /// <summary>One lap of the ring, in seconds.</summary>
    public const double PatrolSeconds = 40;

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

    /// <summary>The poison plant and the lich stay on their posts. Everyone else walks the ring.</summary>
    public static bool StandsStill(int level) => Rooted.Contains(level);

    /// <summary>Where this den's ring sits, in tiles.</summary>
    public static double PostRadius(int level) => StandsStill(level) ? Ranged.GetValueOrDefault(level, MeleeRange) : PatrolRadius;

    /// <summary>How far a protector of this den sees. One that cannot walk sees only as far as it can strike.</summary>
    public static double SightOf(int level) => StandsStill(level) ? Math.Min(Sight, PostRadius(level)) : Sight;

    /// <summary>
    /// Where protector <paramref name="index"/> is at rest. The pack is spaced evenly around the den and, unless it
    /// cannot walk, turns once per <see cref="PatrolSeconds"/>. The web client's <c>patrolAt</c> draws the same round.
    /// </summary>
    public static TilePoint PatrolAt(TilePoint center, int index, int count, int level, DateTimeOffset now)
    {
        var turn = StandsStill(level) ? 0 : now.ToUnixTimeMilliseconds() / 1000.0 / PatrolSeconds * 2 * Math.PI;
        var angle = turn + index * 2 * Math.PI / Math.Max(1, count);
        var radius = PostRadius(level);
        return new TilePoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
    }

    /// <summary>A hero here meets the den: inside the ring, or within sight of a protector on it. A roaming foe does not.</summary>
    public static bool Alerts(SpawnPoint point, TilePoint position, DateTimeOffset now)
    {
        if (point.Roams)
        {
            return false;
        }

        var center = point.Center;
        if (position.DistanceTo(center) <= PostRadius(point.Level))
        {
            return true;
        }

        var count = Math.Max(1, point.Protectors);
        var sight = SightOf(point.Level);
        for (var index = 0; index < count; index++)
        {
            if (position.DistanceTo(PatrolAt(center, index, count, point.Level, now)) <= sight)
            {
                return true;
            }
        }

        return false;
    }
}
