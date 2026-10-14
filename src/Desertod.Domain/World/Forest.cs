using Desertod.Domain.Entities;
using Desertod.Domain.Weather;

namespace Desertod.Domain.World;

/// <summary>
/// One tree on the camp land. A tree is chopped a little every cycle and falls once all its wood is taken;
/// its stump grows back unless something is built over it.
/// </summary>
public sealed class Tree
{
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>Wood already taken from the standing tree.</summary>
    public int Chopped { get; set; }

    /// <summary>When the tree fell; null while it stands.</summary>
    public DateTimeOffset? FelledAt { get; set; }

    public TileArea Tile => new(X, Y, 1, 1);
}

/// <summary>
/// Trees on the camp land west of the river. The server owns them, because they block building and wood
/// gathering fells them; the web client draws the wild beyond this area by itself.
/// </summary>
public static class Forest
{
    public const int WoodPerTree = 15;

    /// <summary>The workplace whose wood comes from these trees.</summary>
    public const string Workplace = "forest";

    public static TimeSpan RegrowTime { get; } = TimeSpan.FromHours(24);

    /// <summary>Camp land up to the river; the web client mirrors it.</summary>
    public static TileArea Area { get; } = new(0, 0, 30, 26);

    /// <summary>Groves planted on new land, with the share of tiles that get a tree. The web client used the same ones.</summary>
    private static readonly (TileArea Area, double Density)[] Groves =
    [
        (new TileArea(0, 0, 8, 12), 0.8),
        (new TileArea(0, 21, 17, 5), 0.55),
        (new TileArea(0, 0, 30, 4), 0.3),
        (new TileArea(8, 4, 22, 17), 0.08),
        (new TileArea(17, 21, 13, 5), 0.3),
    ];

    /// <summary>Ground that never grows trees: the clearing and the bare plot east of it, the trail to the gate, stones, berry bushes and reeds.</summary>
    private static readonly TileArea[] Bare =
    [
        new(12, 10, 7, 7),
        new(15, 8, 3, 4),
        new(0, 12, 12, 3),
        new(17, 12, WorldMap.PlotWidth, WorldMap.PlotHeight),
        new(1, 16, 7, 5),
        new(28, 14, 2, 9),
        new(9, 1, 3, 1),
        new(9, 2, 1, 1),
        new(11, 2, 2, 1),
        new(8, 3, 1, 1),
        new(10, 3, 1, 1),
        new(12, 4, 1, 1),
    ];

    /// <summary>Trees of new land, different for every village.</summary>
    public static List<Tree> Plant(Random random)
    {
        var planted = new HashSet<(int X, int Y)>();
        var trees = new List<Tree>();
        foreach (var (area, density) in Groves)
        {
            for (var y = area.Y; y < area.Y + area.Height; y++)
            {
                for (var x = area.X; x < area.X + area.Width; x++)
                {
                    var tile = new TileArea(x, y, 1, 1);
                    if (random.NextDouble() > density
                        || !Area.Contains(tile)
                        || Bare.Any(bare => bare.Overlaps(tile))
                        || WorldMap.ReservedAreas.Any(reserved => reserved.Overlaps(tile))
                        || !planted.Add((x, y)))
                    {
                        continue;
                    }

                    trees.Add(new Tree { X = x, Y = y });
                }
            }
        }

        return trees;
    }

    /// <summary>A felled tree stands again once its stump had time to grow, unless a building covers it.</summary>
    public static bool Stands(Village village, Tree tree, DateTimeOffset now) =>
        tree.FelledAt is not { } felledAt || (Regrown(village, felledAt, now) && !village.AreaTaken(tree.Tile));

    /// <summary>Rain makes stumps grow back faster.</summary>
    private static bool Regrown(Village village, DateTimeOffset felledAt, DateTimeOffset now)
    {
        if (now >= felledAt + RegrowTime)
        {
            return true;
        }

        if (now <= felledAt)
        {
            return false;
        }

        var grown = (now - felledAt).TotalHours + WeatherRules.RainHours(village, felledAt, now) * (WeatherRules.ForestRegrowth - 1);
        return grown >= RegrowTime.TotalHours;
    }

    /// <summary>Trees standing in the open: the ones drawn on the map and chopped by citizens.</summary>
    public static IEnumerable<Tree> Visible(Village village, DateTimeOffset now) =>
        village.Trees.Where(tree => Stands(village, tree, now) && !village.AreaTaken(tree.Tile));

    /// <summary>
    /// Trees drawn on the map: the visible ones and the ones still standing on a construction site,
    /// waiting to be felled by its builders.
    /// </summary>
    public static IEnumerable<Tree> Drawn(Village village, DateTimeOffset now) =>
        village.Trees.Where(tree => Stands(village, tree, now) && !village.AreaBuilt(tree.Tile));

    /// <summary>Standing trees on the tiles of the area.</summary>
    public static IEnumerable<Tree> Standing(Village village, TileArea area, DateTimeOffset now) =>
        village.Trees.Where(tree => area.Overlaps(tree.Tile) && Stands(village, tree, now));

    /// <summary>
    /// Trees standing where a building is planned stay standing until its builders fell them,
    /// so regrown stumps stop counting as felled. Returns how many trees stand there.
    /// </summary>
    public static int Hold(Village village, TileArea area, DateTimeOffset now)
    {
        var standing = Standing(village, area, now).ToList();
        foreach (var tree in standing)
        {
            tree.FelledAt = null;
        }

        return standing.Count;
    }

    /// <summary>
    /// Builders fell the trees on a construction site's area, taking up to <paramref name="wood"/> from them.
    /// Returns the wood taken; it is less when the trees had been chopped before the site was planned.
    /// </summary>
    public static int Fell(Village village, TileArea area, int wood, DateTimeOffset now)
    {
        var taken = 0;
        foreach (var tree in Standing(village, area, now).OrderBy(tree => tree.Y).ThenBy(tree => tree.X).ToList())
        {
            if (wood - taken <= 0)
            {
                break;
            }

            var share = Math.Min(wood - taken, WoodPerTree - tree.Chopped);
            tree.Chopped += share;
            taken += share;
            if (tree.Chopped >= WoodPerTree)
            {
                tree.FelledAt = now;
                tree.Chopped = 0;
            }
        }

        return taken;
    }

    /// <summary>
    /// Takes the gathered wood from the trees nearest the spot the citizen picked (the forest workplace by default),
    /// felling every tree that runs out. Wood from the wild beyond the camp land, or from bare camp land, fells no tree.
    /// </summary>
    public static void Chop(Village village, int wood, DateTimeOffset now, TilePoint? spot = null)
    {
        if (spot is { } wild && !Area.Contains(wild.ToTile()))
        {
            return;
        }

        var place = spot ?? WorldMap.Places[Workplace];
        var nearest = Visible(village, now)
            .OrderBy(tree => new TilePoint(tree.X + 0.5, tree.Y + 0.5).DistanceTo(place))
            .ThenBy(tree => tree.Y)
            .ThenBy(tree => tree.X);
        foreach (var tree in nearest)
        {
            if (wood <= 0)
            {
                break;
            }

            if (tree.FelledAt is not null)
            {
                tree.FelledAt = null;
                tree.Chopped = 0;
            }

            var taken = Math.Min(wood, WoodPerTree - tree.Chopped);
            tree.Chopped += taken;
            wood -= taken;
            if (tree.Chopped >= WoodPerTree)
            {
                tree.FelledAt = now;
                tree.Chopped = 0;
            }
        }
    }
}
