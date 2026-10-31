namespace Desertod.Domain.World;

/// <summary>
/// Lone foes haunting the land between the den rings. The land is cut into squares of <see cref="Cell"/> tiles; each
/// square holds one foe at a seeded random tile. Its band is the highest ring whose floor that tile reaches; the foe
/// is of the band's level, or one level below or above it by <see cref="Neighbour"/> each. Nothing is stored: the same
/// seed always places the same foes, and the browser's <c>haunts.ts</c> places them on the same tiles.
/// A foe fights as a den with one protector of its level, standing on its own tile.
/// </summary>
public static class Haunts
{
    /// <summary>Side of the square that holds one foe, in tiles.</summary>
    public const int Cell = 64;

    /// <summary>Share of foes one level below the band, and again one level above it.</summary>
    public const double Neighbour = 0.05;

    /// <summary>No foe stands this close to a den, in tiles, so it never joins a den fight.</summary>
    public const double DenClearance = 40;

    /// <summary>Side of a foe's claim: it gives up a chase half this far from its tile.</summary>
    public const int Claim = 32;

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

    /// <summary>Foes whose tile lies within <paramref name="radius"/> of <paramref name="position"/>.</summary>
    public static IReadOnlyList<SpawnPoint> Near(uint seed, TilePoint position, double radius, IReadOnlyList<SpawnPoint> dens)
    {
        var found = new List<SpawnPoint>();
        for (var cellY = (int)Math.Floor((position.Y - radius) / Cell); cellY <= (int)Math.Floor((position.Y + radius) / Cell); cellY++)
        {
            for (var cellX = (int)Math.Floor((position.X - radius) / Cell); cellX <= (int)Math.Floor((position.X + radius) / Cell); cellX++)
            {
                if (At(seed, cellX, cellY, dens) is { } foe && foe.Center.DistanceTo(position) <= radius)
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
