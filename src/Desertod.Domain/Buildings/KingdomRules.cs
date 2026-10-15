using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Domain.Buildings;

public static class KingdomRules
{
    public static int Level(Village village) => village.Structures.Where(s => s.Kind == BuildingKind.Castle && !s.Decommissioning)
        .Select(s => s.Level).DefaultIfEmpty(0).Max();

    // Grow west, north and south. The river and its bridge remain independent gates.
    public static TileArea Area(Village village) => Level(village) switch
    {
        >= 3 => new(-8, -6, 36, 38),
        2 => new(-4, -3, 32, 32),
        _ => WorldMap.BuildArea,
    };
    public static bool Inside(Village village, string plot) => WorldMap.Footprint(plot) is { } area && Area(village).Contains(area);

    public static bool ClearGround(Village village, TileArea area)
    {
        var seed = Wilderness.Seed(village.Id.ToString());
        for (var x=area.X; x<area.X+area.Width; x++)
        for (var y=area.Y; y<area.Y+area.Height; y++)
        {
            if (Math.Abs(x+.5-Wilderness.RiverCenter(y,seed)) < 2) return false;
            if (village.Wilds?.Ravine.Any(r=>r.X==x && y>=r.Top && y<r.Top+r.Height)==true) return false;
            if (village.Wilds?.Obstacles.Any(o=>o.X==x && o.Y==y && Wilderness.Stands(o))==true) return false;
        }
        return true;
    }
}
