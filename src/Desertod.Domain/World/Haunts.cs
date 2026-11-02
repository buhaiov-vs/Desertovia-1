using Desertod.Domain.Combat;

namespace Desertod.Domain.World;

/// <summary>
/// Lone foes haunting the land around the village and between the den rings. The land is cut into squares of
/// <see cref="Cell"/> tiles; each square holds one foe at a seeded random tile. Its band is the highest ring whose floor
/// that tile reaches, and the first band inside the first ring, so level 1 foes roam right outside the vale; the foe
/// is of the band's level, one level below it by <see cref="Below"/>, or up to four levels above it by
/// <see cref="Above"/>, so a careless pull can wake a much stronger foe. Nothing is stored: the same
/// seed always places the same foes, and the browser's <c>haunts.ts</c> places them on the same tiles.
/// A foe wanders around its tile (<see cref="PositionAt"/>) and fights as a den with one protector of its level,
/// starting where it walked; after the fight it goes back there and wanders on.
/// </summary>
public static class Haunts
{
    /// <summary>Side of the square that holds one foe, in tiles: about two foes for every 64 × 64.</summary>
    public const int Cell = 45;

    /// <summary>Share of foes one level below the band.</summary>
    public const double Below = 0.05;

    /// <summary>Levels above the band and the share of foes of each: the further above, the rarer.</summary>
    public static IReadOnlyList<(int Levels, double Share)> Above { get; } = [(4, 0.03), (3, 0.06), (2, 0.09), (1, 0.12)];

    /// <summary>No foe stands this close to a den, in tiles, so it never joins a den fight.</summary>
    public const double DenClearance = 40;

    /// <summary>Side of a foe's claim: it gives up a chase half this far from where the chase began.</summary>
    public const int Claim = 32;

    /// <summary>A foe wanders this far from its tile at most, in tiles.</summary>
    public const double WanderRadius = 8;

    /// <summary>Every this many seconds a foe sets off to a new spot of its wander.</summary>
    public const double LegSeconds = 24;

    /// <summary>Strolling pace, in tiles a second; a long leg is walked faster so it ends within three quarters of the leg.</summary>
    public const double WanderSpeed = 0.5;

    /// <summary>
    /// Where a foe is at <paramref name="now"/>, at rest: leg k walks from the spot of leg k − 1 to the spot of leg k,
    /// then waits. The spots are fixed by the seed, the square and the leg, so the browser draws the same walk.
    /// </summary>
    public static TilePoint PositionAt(uint seed, SpawnPoint foe, DateTimeOffset now)
    {
        var seconds = now.ToUnixTimeMilliseconds() / 1000.0;
        var leg = (int)Math.Floor(seconds / LegSeconds);
        var (cellX, cellY) = ((int)Math.Floor((double)foe.X / Cell), (int)Math.Floor((double)foe.Y / Cell));
        var from = Waypoint(seed, cellX, cellY, foe.Center, leg - 1);
        var to = Waypoint(seed, cellX, cellY, foe.Center, leg);
        var length = from.DistanceTo(to);
        var walk = Math.Min(length / WanderSpeed, LegSeconds * 0.75);
        var share = walk <= 0 ? 1 : Math.Min(1, (seconds - leg * LegSeconds) / walk);
        return new TilePoint(from.X + (to.X - from.X) * share, from.Y + (to.Y - from.Y) * share);
    }

    private static TilePoint Waypoint(uint seed, int cellX, int cellY, TilePoint home, int leg)
    {
        var angle = Roll(seed, cellX, cellY, 3 + 2 * leg) * 2 * Math.PI;
        var radius = Math.Sqrt(Roll(seed, cellX, cellY, 4 + 2 * leg)) * WanderRadius;
        return new TilePoint(home.X + Math.Cos(angle) * radius, home.Y + Math.Sin(angle) * radius);
    }

    /// <summary>
    /// No foe stands this close to the vale, in tiles: its wander plus the reach that wakes it, so it never walks into
    /// the village nor wakes for a citizen inside it.
    /// </summary>
    public const double ValeClearance = WanderRadius + DenGuards.AggroRange;

    /// <summary>
    /// The foe of one square; null when its tile is off the land, near a den or near the vale, or when water, a mountain or
    /// a thicket lies within its wander, so it never stands on or walks into one. Between the vale
    /// and the first ring the foes are of the first band, so level 1 foes roam around the village.
    /// </summary>
    public static SpawnPoint? At(uint seed, int cellX, int cellY, IReadOnlyList<SpawnPoint> dens)
    {
        var x = cellX * Cell + (int)Math.Floor(Roll(seed, cellX, cellY, 0) * Cell);
        var y = cellY * Cell + (int)Math.Floor(Roll(seed, cellX, cellY, 1) * Cell);
        if (!WorldMap.Land.Contains(new TileArea(x, y, 1, 1)))
        {
            return null;
        }

        // A foe never stands on or wanders into water, a mountain or a thicket.
        var reach = (int)WanderRadius + 1;
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                if (dx * dx + dy * dy <= reach * reach && FarWilds.Taken(x + dx, y + dy, seed))
                {
                    return null;
                }
            }
        }

        var center = new TilePoint(x + 0.5, y + 0.5);
        var distance = center.DistanceTo(WorldMap.Places[WorldMap.Camp]);
        var levels = SpawnRings.Levels;
        var band = 1;
        while (band < levels.Count && levels[band].Floor <= distance)
        {
            band++;
        }

        if (FromVale(center) < ValeClearance || dens.Any(den => den.Center.DistanceTo(center) < DenClearance))
        {
            return null;
        }

        var ring = levels[LevelOf(band, Roll(seed, cellX, cellY, 2)) - 1];
        return new SpawnPoint($"h:{cellX}:{cellY}", ring.Level, ring.Name, ring.Kind, 1, Claim, x, y, Roams: true);
    }

    /// <summary>The level of a foe of <paramref name="band"/> for a <paramref name="mix"/> in [0, 1), within the known levels.</summary>
    public static int LevelOf(int band, double mix)
    {
        if (mix < Below)
        {
            return Math.Max(1, band - 1);
        }

        var share = Below;
        foreach (var (levels, chance) in Above)
        {
            share += chance;
            if (mix < share)
            {
                return Math.Min(SpawnRings.Levels.Count, band + levels);
            }
        }

        return band;
    }

    /// <summary>Foes that wander within <paramref name="radius"/> of <paramref name="position"/> at <paramref name="now"/>.</summary>
    public static IReadOnlyList<SpawnPoint> Near(uint seed, TilePoint position, double radius, IReadOnlyList<SpawnPoint> dens, DateTimeOffset now)
    {
        var found = new List<SpawnPoint>();
        var reach = radius + WanderRadius;
        for (var cellY = (int)Math.Floor((position.Y - reach) / Cell); cellY <= (int)Math.Floor((position.Y + reach) / Cell); cellY++)
        {
            for (var cellX = (int)Math.Floor((position.X - reach) / Cell); cellX <= (int)Math.Floor((position.X + reach) / Cell); cellX++)
            {
                if (At(seed, cellX, cellY, dens) is { } foe && PositionAt(seed, foe, now).DistanceTo(position) <= radius)
                {
                    found.Add(foe);
                }
            }
        }

        return found;
    }

    /// <summary>Distance from <paramref name="point"/> to the nearest tile of the vale, in tiles; 0 inside it.</summary>
    public static double FromVale(TilePoint point)
    {
        var vale = WorldMap.Vale;
        var dx = Math.Max(Math.Max(vale.X - point.X, 0), point.X - (vale.X + vale.Width));
        var dy = Math.Max(Math.Max(vale.Y - point.Y, 0), point.Y - (vale.Y + vale.Height));
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>A number in [0, 1) fixed by the seed, the square and the draw. The browser computes the same bits.</summary>
    private static double Roll(uint seed, int cellX, int cellY, int draw)
    {
        unchecked
        {
            var hash = seed ^ ((uint)cellX * 0x27D4EB2Fu) ^ ((uint)cellY * 0x165667B1u) ^ ((uint)(draw + 1) * 0x9E3779B1u);
            hash ^= hash >> 15;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return hash / 4_294_967_296.0;
        }
    }
}
