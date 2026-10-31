namespace Desertod.Domain.World;

/// <summary>
/// Lone foes haunting the land between the den rings. The land is cut into squares of <see cref="Cell"/> tiles; each
/// square holds one foe at a seeded random tile. Its band is the highest ring whose floor that tile reaches; the foe
/// is of the band's level, or one level below or above it by <see cref="Neighbour"/> each. Nothing is stored: the same
/// seed always places the same foes, and the browser's <c>haunts.ts</c> places them on the same tiles.
/// A foe wanders around its tile (<see cref="PositionAt"/>) and fights as a den with one protector of its level,
/// starting where it walked; after the fight it goes back there and wanders on.
/// </summary>
public static class Haunts
{
    /// <summary>Side of the square that holds one foe, in tiles: about two foes for every 64 × 64.</summary>
    public const int Cell = 45;

    /// <summary>Share of foes one level below the band, and again one level above it.</summary>
    public const double Neighbour = 0.05;

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

    /// <summary>The foe of one square; null when its tile is closer to the camp than the first ring, off the land, on water or near a den.</summary>
    public static SpawnPoint? At(uint seed, int cellX, int cellY, IReadOnlyList<SpawnPoint> dens)
    {
        var x = cellX * Cell + (int)Math.Floor(Roll(seed, cellX, cellY, 0) * Cell);
        var y = cellY * Cell + (int)Math.Floor(Roll(seed, cellX, cellY, 1) * Cell);
        if (!WorldMap.Land.Contains(new TileArea(x, y, 1, 1)) || FarGround.Water(x, y, seed))
        {
            return null;
        }

        var center = new TilePoint(x + 0.5, y + 0.5);
        var distance = center.DistanceTo(WorldMap.Places[WorldMap.Camp]);
        var levels = SpawnRings.Levels;
        var band = 0;
        while (band < levels.Count && levels[band].Floor <= distance)
        {
            band++;
        }

        if (band == 0 || dens.Any(den => den.Center.DistanceTo(center) < DenClearance))
        {
            return null;
        }

        var mix = Roll(seed, cellX, cellY, 2);
        var level = mix < Neighbour ? Math.Max(1, band - 1) : mix < 2 * Neighbour ? Math.Min(levels.Count, band + 1) : band;
        var ring = levels[level - 1];
        return new SpawnPoint($"h:{cellX}:{cellY}", ring.Level, ring.Name, ring.Kind, 1, Claim, x, y, Roams: true);
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
