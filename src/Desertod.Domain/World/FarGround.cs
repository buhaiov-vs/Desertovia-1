namespace Desertod.Domain.World;

/// <summary>
/// Ground outside the camp picture. A tile is a function of the village seed and its own coordinate, so a chunk
/// can be drawn without storing the rest of the land. Rivers are the water courses in <see cref="FarWilds"/>.
/// The camp picture stays dry here; its own river is authored terrain.
/// </summary>
public static class FarGround
{
    public const int RiverWidth = 4;

    /// <summary>The western water tile of the river in this row. Matches the web client's <c>Math.round</c> for these values.</summary>
    public static int RiverLeft(int y, uint seed) => (int)Math.Round(Wilderness.RiverCenter(y, seed) - 1.5, MidpointRounding.AwayFromZero);

    public static bool Water(int x, int y, uint seed) => FarWilds.At(x, y, seed) == FarWilds.Kind.Water;
}
