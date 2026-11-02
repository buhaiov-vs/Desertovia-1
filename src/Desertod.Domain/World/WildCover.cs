namespace Desertod.Domain.World;

/// <summary>
/// Woods of the open land. The web client's <c>wildTree</c> uses these same rolls,
/// inside the camp picture and past it.
/// </summary>
public static class WildCover
{
    /// <summary>True when a tree stands on the tile. The campfire's own circle stays clear.</summary>
    public static bool Tree(uint seed, int x, int y)
    {
        var distance = Math.Sqrt(Math.Pow(x + 0.5 - 15.5, 2) + Math.Pow(y + 0.5 - 13.5, 2));
        if (distance < 8)
        {
            return false;
        }

        var forest = Noise(unchecked(seed + 1), 9, x, y) * 0.75 + Noise(unchecked(seed + 2), 3, x, y) * 0.25;
        var roll = Roll(unchecked(seed + 5), x, y);
        if (forest > 0.64)
        {
            return roll < 0.18;
        }

        if (forest > 0.54)
        {
            return roll < 0.05;
        }

        return roll < 0.012;
    }

    private static double Roll(uint seed, int x, int y)
    {
        var value = seed ^ unchecked((uint)(x * 0x27d4eb2d)) ^ unchecked((uint)(y * 0x165667b1));
        value = unchecked((value ^ (value >> 16)) * 0x85ebca6b);
        value = unchecked((value ^ (value >> 13)) * 0xc2b2ae35);
        return (value ^ (value >> 16)) / 4294967296d;
    }

    private static double Noise(uint seed, double scale, double x, double y)
    {
        double Corner(int cx, int cy)
        {
            var value = seed ^ unchecked((uint)(cx * 0x27d4eb2d)) ^ unchecked((uint)(cy * 0x165667b1));
            value = unchecked((value ^ (value >> 16)) * 0x85ebca6b);
            value = unchecked((value ^ (value >> 13)) * 0xc2b2ae35);
            return (value ^ (value >> 16)) / 4294967296d;
        }

        static double Smooth(double t) => t * t * (3 - 2 * t);
        var fx = x / scale;
        var fy = y / scale;
        var x0 = (int)Math.Floor(fx);
        var y0 = (int)Math.Floor(fy);
        var tx = Smooth(fx - x0);
        var ty = Smooth(fy - y0);
        var top = Corner(x0, y0) * (1 - tx) + Corner(x0 + 1, y0) * tx;
        var bottom = Corner(x0, y0 + 1) * (1 - tx) + Corner(x0 + 1, y0 + 1) * tx;
        return top * (1 - ty) + bottom * ty;
    }
}
