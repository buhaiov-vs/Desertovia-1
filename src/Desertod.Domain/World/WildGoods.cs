namespace Desertod.Domain.World;

/// <summary>
/// Coloured gathering patches in the open arcs between the dens of one foe ring.
/// A patch is a function of the village seed, so the land does not store one.
/// The browser's <c>wildGoods.ts</c> places the same tiles.
/// </summary>
public static class WildGoods
{
    public const string Herbs = "herbs";
    public const string Flint = "flint";
    public const string Clay = "clay";
    public const string Salt = "salt";
    public const string Resin = "resin";
    public const string Copper = "copper";

    /// <summary>How far past a den's claim a patch must stand, in tiles.</summary>
    public const int ClearOfDen = 10;

    /// <summary>Patches stay inside this distance from the camp, short of the land's edge.</summary>
    public const int Reach = 5_600;

    public readonly record struct Patch(int X, int Y, string Kind, int Level);

    private static readonly Dictionary<uint, IReadOnlyList<Patch>> Cached = new();
    private static readonly object Gate = new();

    public static bool IsPlace(string workplace) =>
        workplace is Herbs or Flint or Clay or Salt or Resin or Copper;

    /// <summary>Which good grows on the ring of that foe level.</summary>
    public static string KindFor(int level) => level switch
    {
        <= 3 => Herbs,
        <= 6 => Flint,
        <= 9 => Clay,
        <= 12 => Salt,
        <= 16 => Resin,
        _ => Copper,
    };

    public static IReadOnlyList<Patch> Patches(uint seed)
    {
        lock (Gate)
        {
            if (Cached.TryGetValue(seed, out var ready))
            {
                return ready;
            }

            var built = Build(seed);
            Cached[seed] = built;
            return built;
        }
    }

    /// <summary>The good on this tile, or null when the tile is empty ground.</summary>
    public static string? At(int x, int y, uint seed)
    {
        foreach (var patch in Patches(seed))
        {
            if (x >= patch.X && x < patch.X + 2 && y >= patch.Y && y < patch.Y + 2)
            {
                return patch.Kind;
            }
        }

        return null;
    }

    private static List<Patch> Build(uint seed)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        var dens = SpawnRings.Points(seed);
        var patches = new List<Patch>();
        foreach (var level in SpawnRings.Levels)
        {
            var radius = level.Floor + SpawnRings.Spread / 2.0;
            if (radius > Reach)
            {
                break;
            }

            var kind = KindFor(level.Level);
            for (var direction = 0; direction < SpawnRings.PointsOnARing; direction++)
            {
                var angle = (direction + 0.5) * Math.PI / 4;
                var roll = FarWilds.Roll(seed, level.Level, direction, 1);
                var mid = radius + (roll - 0.5) * 36;
                if (Try(camp, angle, mid, seed, dens, patches, out var x, out var y)
                    || Try(camp, angle, mid + 28, seed, dens, patches, out x, out y)
                    || Try(camp, angle, mid - 28, seed, dens, patches, out x, out y)
                    || Try(camp, angle, mid + 56, seed, dens, patches, out x, out y)
                    || Try(camp, angle, mid - 56, seed, dens, patches, out x, out y)
                    || Try(camp, angle, mid + 84, seed, dens, patches, out x, out y)
                    || Try(camp, angle, mid - 84, seed, dens, patches, out x, out y))
                {
                    patches.Add(new Patch(x, y, kind, level.Level));
                }
            }
        }

        return patches;
    }

    private static bool Try(
        TilePoint camp,
        double angle,
        double radius,
        uint seed,
        IReadOnlyList<SpawnPoint> dens,
        List<Patch> placed,
        out int x,
        out int y)
    {
        x = (int)Math.Floor(camp.X + Math.Cos(angle) * radius);
        y = (int)Math.Floor(camp.Y + Math.Sin(angle) * radius);
        if (!Fits(x, y, seed) || TooClose(x, y, dens) || Overlaps(x, y, placed))
        {
            return false;
        }

        return true;
    }

    private static bool Fits(int x, int y, uint seed)
    {
        for (var dy = 0; dy < 2; dy++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                var tile = new TileArea(x + dx, y + dy, 1, 1);
                var kind = FarWilds.At(x + dx, y + dy, seed);
                if (!WorldMap.Land.Contains(tile) || WorldMap.Vale.Contains(tile) || FarWilds.Blocks(kind) || kind == FarWilds.Kind.Water)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TooClose(int x, int y, IReadOnlyList<SpawnPoint> dens)
    {
        foreach (var den in dens)
        {
            var reach = den.Claim / 2 + ClearOfDen;
            var left = den.X - x;
            var top = den.Y - y;
            if (left * left + top * top < reach * reach)
            {
                return true;
            }

            var right = den.X - (x + 1);
            var bottom = den.Y - (y + 1);
            if (right * right + bottom * bottom < reach * reach)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Overlaps(int x, int y, List<Patch> placed)
    {
        foreach (var patch in placed)
        {
            if (Math.Abs(patch.X - x) < 2 && Math.Abs(patch.Y - y) < 2)
            {
                return true;
            }
        }

        return false;
    }
}
