namespace Desertod.Domain.World;

/// <summary>
/// The stone cave. Wood and stone stay where they already are. The mouth is not in the vale:
/// the village seed picks one tile between a 6-minute walk and a 1-hour walk, and that tile never moves.
/// Coal, iron and moonstone are further mouths from the same seed, kept apart from this one.
/// </summary>
public readonly record struct CaveTile(int X, int Y)
{
    public TilePoint Mouth => new(X + 0.5, Y + 0.5);
}

public static class CaveSite
{
    /// <summary>Closest the mouth may stand, in tiles. A healthy walker takes 6 minutes. Wood groves start here too.</summary>
    public const int Near = 600;

    /// <summary>Farthest the mouth may stand, in tiles. A healthy walker takes 1 hour. The stone hill and the forest stay closer.</summary>
    public const int Far = 6_000;

    private const int Tries = 48;

    /// <summary>How far, in tiles, two mine mouths stay apart.</summary>
    public const int Apart = 180;

    /// <summary>
    /// The mouth of this village. The same seed always returns the same tile.
    /// A non-zero <paramref name="salt"/> picks another mouth. <paramref name="apartFrom"/> keeps it off those mouths and off a mountain band.
    /// </summary>
    public static CaveTile At(uint seed, uint salt = 0, IReadOnlyList<CaveTile>? apartFrom = null)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        var dens = SpawnRings.Points(seed);
        for (var attempt = 0; attempt < Tries; attempt++)
        {
            var radius = Radius(seed, attempt, salt);
            var angle = Roll(seed, attempt * 2 + 1, salt) * 2 * Math.PI;
            var x = (int)Math.Floor(camp.X + Math.Cos(angle) * radius);
            var y = (int)Math.Floor(camp.Y + Math.Sin(angle) * radius);
            if (Free(x, y, seed, dens, Far, apartFrom))
            {
                return new CaveTile(x, y);
            }
        }

        var fallback = Roll(seed, 1, salt) * 2 * Math.PI;
        for (var radius = Near; radius < WorldMap.Side / 2; radius += 50)
        {
            var x = (int)Math.Floor(camp.X + Math.Cos(fallback) * radius);
            var y = (int)Math.Floor(camp.Y + Math.Sin(fallback) * radius);
            if (Free(x, y, seed, dens, WorldMap.Side, apartFrom))
            {
                return new CaveTile(x, y);
            }
        }

        var nudge = salt == 0 ? 0 : (int)(salt % 400);
        return new CaveTile((int)Math.Floor(camp.X + Near), (int)Math.Floor(camp.Y) + nudge);
    }

    /// <summary>A radius in [<see cref="Near"/>, <see cref="Far"/>], spread evenly over the ring's area.</summary>
    private static double Radius(uint seed, int attempt, uint salt)
    {
        var span = (double)Far * Far - (double)Near * Near;
        return Math.Sqrt((double)Near * Near + Roll(seed, attempt * 2, salt) * span);
    }

    private static bool Free(int x, int y, uint seed, IReadOnlyList<SpawnPoint> dens, int farthest, IReadOnlyList<CaveTile>? apartFrom)
    {
        var tile = new TileArea(x, y, 1, 1);
        if (!WorldMap.Land.Contains(tile) || WorldMap.Vale.Contains(tile) || FarGround.Water(x, y, seed))
        {
            return false;
        }

        var center = new TilePoint(x + 0.5, y + 0.5);
        var camp = WorldMap.Places[WorldMap.Camp];
        var distance = center.DistanceTo(camp);
        if (distance < Near || distance > farthest)
        {
            return false;
        }

        if (apartFrom is not null && (FarWilds.Blocks(FarWilds.At(x, y, seed)) || apartFrom.Any(other => other.Mouth.DistanceTo(center) < Apart)))
        {
            return false;
        }

        return dens.All(den => den.Center.DistanceTo(center) >= den.Claim / 2.0);
    }

    /// <summary>A number in [0, 1). The browser's <c>caveRoll</c> computes the same bits when <paramref name="salt"/> is 0.</summary>
    private static double Roll(uint seed, int draw, uint salt)
    {
        unchecked
        {
            var hash = seed ^ 0xC0FFEE01u ^ ((uint)(draw + 1) * 0x9E3779B1u);
            if (salt != 0)
            {
                hash ^= salt;
            }
            hash ^= hash >> 16;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return hash / 4_294_967_296.0;
        }
    }
}
