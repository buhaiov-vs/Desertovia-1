using Desertod.Domain.Combat;

namespace Desertod.Domain.World;

/// <summary>One foe ring. The cave place and the renewal goods are not part of this list.</summary>
public sealed record SpawnLevel(int Level, string Name, int Floor, int Protectors, EnemyKind? Kind)
{
    public int Claim => Protectors > 1 ? SpawnRings.PackClaim : SpawnRings.SingleClaim;

    /// <summary>Health of one protector, from the existing fight catalog. A foe with no catalog entry has none yet.</summary>
    public double? ProtectorHealth => Kind is { } kind ? 12 + EnemyCatalog.Get(kind).Power * 2 : null;
}

/// <summary>One den on a ring. <see cref="Id"/> is the level and the direction, so a destroyed den can be named again.</summary>
public sealed record SpawnPoint(string Id, int Level, string Name, EnemyKind? Kind, int Protectors, int Claim, int X, int Y)
{
    public TilePoint Center => new(X + 0.5, Y + 0.5);
}

/// <summary>A citizen standing on one of the eight spots around a den.</summary>
public sealed record SpotPresence(int Spot, Guid CitizenId, DateTimeOffset Since);

/// <summary>
/// The thirty foe rings around the camp, eight points each. A point lands at a seeded random place in a wide band:
/// from its floor up to <see cref="Spread"/> tiles further out, and up to <see cref="Sway"/> either side of its direction.
/// When no such place is dry land, it falls back to the ring itself: a point past the land is left out,
/// so past the straight edge only the four corner points remain, and a point in the river slides along the ring
/// to the nearest dry tile that is still at least that foe's floor away.
/// </summary>
public static class SpawnRings
{
    public const int PointsOnARing = 8;
    public const int Spots = 8;
    public const int PackClaim = 48;
    public const int SingleClaim = 64;
    public static readonly TimeSpan Hold = TimeSpan.FromSeconds(3);

    /// <summary>A den lands up to this many tiles past its floor; the next ring starts 541 tiles out.</summary>
    public const int Spread = 400;

    /// <summary>A den turns up to this far either side of its direction, in radians (18 degrees).</summary>
    public const double Sway = Math.PI / 10;

    /// <summary>Random places tried before the den falls back to its own ring.</summary>
    private const int ScatterTries = 6;

    /// <summary>The cave has no chosen tile. It is not the cave in the home vale.</summary>
    public static TilePoint? CavePlace => null;

    public static IReadOnlyList<SpawnLevel> Levels { get; } =
    [
        Level(1, "Plain slime", 600, 3, null),
        Level(2, "Grave rat", 1_141, 4, EnemyKind.Rat),
        Level(3, "Imp", 1_682, 2, EnemyKind.Imp),
        Level(4, "Small goblin", 2_223, 3, null),
        Level(5, "Wolf", 2_764, 4, EnemyKind.Wolf),
        Level(6, "Boar", 3_305, 3, EnemyKind.Boar),
        Level(7, "Goblin with a club", 3_846, 3, null),
        Level(8, "Hooded orc", 4_387, 1, null),
        Level(9, "Scarecrow", 4_928, 1, null),
        Level(10, "Ghoul", 5_469, 2, EnemyKind.Ghoul),
        Level(11, "Goblin with a sword", 6_010, 3, null),
        Level(12, "Banner goblin", 6_551, 3, null),
        Level(13, "Wraith", 7_092, 1, EnemyKind.Wraith),
        Level(14, "Poison plant", 7_633, 1, null),
        Level(15, "Flaming slime", 8_174, 1, null),
        Level(16, "Orc with one axe", 8_715, 1, null),
        Level(17, "Bear", 9_256, 1, EnemyKind.Bear),
        Level(18, "Orc warrior", 9_797, 1, null),
        Level(19, "Vampire", 10_338, 1, null),
        Level(20, "Orc berserk", 10_879, 1, null),
        Level(21, "Orc shaman", 11_420, 1, null),
        Level(22, "Winged vampire", 11_961, 1, null),
        Level(23, "Orc with two swords", 12_502, 1, null),
        Level(24, "Zombie", 13_043, 1, EnemyKind.Zombie),
        Level(25, "Fire spirit", 13_584, 1, null),
        Level(26, "Skeleton", 14_125, 1, EnemyKind.Skeleton),
        Level(27, "Armored orc", 14_666, 1, null),
        Level(28, "Fortress knight", 15_207, 1, null),
        Level(29, "Barrow king", 15_748, 1, EnemyKind.BarrowKing),
        Level(30, "Lich", 16_289, 1, null),
    ];

    /// <summary>The dens of one village. The same seed always places the same dens.</summary>
    public static IReadOnlyList<SpawnPoint> Points(uint seed)
    {
        var points = new List<SpawnPoint>();
        foreach (var level in Levels)
        {
            for (var direction = 0; direction < PointsOnARing; direction++)
            {
                if (TryPlace(seed, level.Level, level.Floor, direction, out var x, out var y))
                {
                    points.Add(new SpawnPoint($"{level.Level}:{direction}", level.Level, level.Name, level.Kind, level.Protectors, level.Claim, x, y));
                }
            }
        }

        return points;
    }

    /// <summary>Level 1 sends from the start. A spotted level N allows every point of level N+1. Allowed levels stay allowed.</summary>
    public static int AllowedThrough(int highestSpottedLevel) => Math.Max(1, highestSpottedLevel + 1);

    /// <summary>Living points of an allowed level. A destroyed point is absent. A foe with no catalog entry is not a raid body yet.</summary>
    public static IReadOnlyList<SpawnPoint> Senders(IReadOnlyList<SpawnPoint> points, Func<int, int, bool> explored, IReadOnlySet<string>? destroyed = null)
    {
        var gone = destroyed ?? new HashSet<string>();
        var spotted = points.Where(point => explored(point.X, point.Y)).Select(point => point.Level).DefaultIfEmpty(0).Max();
        var allowed = AllowedThrough(spotted);
        return points.Where(point => point.Level <= allowed && point.Kind is not null && !gone.Contains(point.Id)).ToList();
    }

    /// <summary>
    /// Where that point's bodies appear: the fog tile on the line closest to the camp.
    /// A line that is already revealed out to the point starts the bodies at the point.
    /// </summary>
    public static TilePoint Appearance(TilePoint camp, TilePoint point, Func<int, int, bool> explored)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(camp.DistanceTo(point)));
        for (var step = 0; step <= steps; step++)
        {
            var share = (double)step / steps;
            var x = (int)Math.Floor(camp.X + (point.X - camp.X) * share);
            var y = (int)Math.Floor(camp.Y + (point.Y - camp.Y) * share);
            if (!explored(x, y))
            {
                return new TilePoint(x + 0.5, y + 0.5);
            }
        }

        return point;
    }

    /// <summary>One body per living point. Its health is one protector, and a body is not split.</summary>
    public static int Bodies(SpawnLevel level) => level.ProtectorHealth is null ? 0 : 1;

    /// <summary>Eight tiles around the den, one step of two tiles out, starting east and turning clockwise.</summary>
    public static IReadOnlyList<TilePoint> SpotsAround(SpawnPoint point)
    {
        var spots = new TilePoint[Spots];
        for (var index = 0; index < Spots; index++)
        {
            var angle = index * Math.PI / 4;
            spots[index] = new TilePoint(point.Center.X + Math.Cos(angle) * 2, point.Center.Y + Math.Sin(angle) * 2);
        }

        return spots;
    }

    /// <summary>
    /// The den falls when its protectors are dead and eight different citizens have stood, one on each spot, for the whole hold.
    /// A citizen who leaves before the hold ends is not in <paramref name="present"/>, so the den stays.
    /// </summary>
    public static bool Falls(bool protectorsDead, IReadOnlyList<SpotPresence> present, DateTimeOffset now)
    {
        if (!protectorsDead || present.Count < Spots)
        {
            return false;
        }

        var spots = new HashSet<int>();
        var citizens = new HashSet<Guid>();
        foreach (var person in present)
        {
            if (person.Spot < 0 || person.Spot >= Spots || now - person.Since < Hold)
            {
                return false;
            }

            spots.Add(person.Spot);
            citizens.Add(person.CitizenId);
        }

        return spots.Count == Spots && citizens.Count == Spots;
    }

    /// <summary>A lich den cannot be renewed. Every other den waits for a resource that is not chosen yet.</summary>
    public static string? RenewalBlock(int level) =>
        level >= Levels.Count ? "A lich point cannot be renewed." : "The renewal resource is not set.";

    /// <summary>Names, amounts, and skill levels of the renewal goods. Empty until that list is chosen.</summary>
    public static IReadOnlyList<string> RenewalGoods => [];

    /// <summary>
    /// The cave sends the foes of destroyed dens, and only when at least one den is destroyed.
    /// It has no tile yet, so it cannot place those foes on a line.
    /// </summary>
    public static IReadOnlyList<string> CaveFoes(IReadOnlyList<SpawnPoint> points, IReadOnlySet<string> destroyed)
    {
        if (destroyed.Count == 0)
        {
            return [];
        }

        return points.Where(point => destroyed.Contains(point.Id)).Select(point => point.Name).Distinct().ToList();
    }

    private static SpawnLevel Level(int level, string name, int floor, int protectors, EnemyKind? kind) =>
        new(level, name, floor, protectors, kind);

    private static bool TryPlace(uint seed, int level, int floor, int direction, out int x, out int y)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        var angle = direction * Math.PI / 4;
        for (var attempt = 0; attempt < ScatterTries; attempt++)
        {
            var sway = (Roll(seed, level, direction, attempt * 2) * 2 - 1) * Sway;
            var radius = floor + Roll(seed, level, direction, attempt * 2 + 1) * Spread;
            if (Near(camp, angle + sway, radius, floor, out x, out y) && WorldMap.Land.Contains(new TileArea(x, y, 1, 1)) && !FarGround.Water(x, y, seed))
            {
                return true;
            }
        }

        if (!OnRing(camp, angle, floor, out x, out y) || !WorldMap.Land.Contains(new TileArea(x, y, 1, 1)))
        {
            x = 0;
            y = 0;
            return false;
        }

        if (!FarGround.Water(x, y, seed))
        {
            return true;
        }

        for (var turn = 1; turn <= 8; turn++)
        {
            foreach (var sign in new[] { 1, -1 })
            {
                if (!OnRing(camp, angle + sign * turn * (Math.PI / 64), floor, out x, out y))
                {
                    continue;
                }

                if (!WorldMap.Land.Contains(new TileArea(x, y, 1, 1)) || FarGround.Water(x, y, seed))
                {
                    continue;
                }

                return true;
            }
        }

        x = 0;
        y = 0;
        return false;
    }

    private static bool OnRing(TilePoint camp, double angle, int floor, out int x, out int y) =>
        Near(camp, angle, floor, floor, out x, out y);

    private static bool Near(TilePoint camp, double angle, double radius, int floor, out int x, out int y)
    {
        x = (int)Math.Floor(camp.X + Math.Cos(angle) * radius);
        y = (int)Math.Floor(camp.Y + Math.Sin(angle) * radius);
        return new TilePoint(x + 0.5, y + 0.5).DistanceTo(camp) + 1 >= floor;
    }

    /// <summary>A number in [0, 1) fixed by the seed, the den and the draw. The browser computes the same bits.</summary>
    private static double Roll(uint seed, int level, int direction, int draw)
    {
        unchecked
        {
            var hash = seed ^ ((uint)level * 0x9E3779B1u) ^ ((uint)(direction + 1) * 0x85EBCA77u) ^ ((uint)(draw + 1) * 0xC2B2AE3Du);
            hash ^= hash >> 16;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return hash / 4_294_967_296.0;
        }
    }
}
