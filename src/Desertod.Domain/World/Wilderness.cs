using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;

namespace Desertod.Domain.World;

public enum ObstacleKind
{
    /// <summary>A long hedge of brambles. Cutting a gap through it is shorter than walking around.</summary>
    Thicket,

    /// <summary>Boulders in a mountain pass. The ridge runs to the edge of the vale, so breaking them opens the way.</summary>
    Rockfall,
}

/// <summary>One tile of a thicket or a rockfall. Nobody walks through it until citizens have worked it away.</summary>
public sealed class Obstacle
{
    public ObstacleKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>Work already done on the tile; it is cleared at <see cref="Wilderness.WorkNeeded"/>.</summary>
    public int Work { get; set; }

    /// <summary>When the tile was cleared; null while it still blocks the way.</summary>
    public DateTimeOffset? ClearedAt { get; set; }

    public TileArea Tile => new(X, Y, 1, 1);

    public TilePoint Center => new(X + 0.5, Y + 0.5);
}

/// <summary>The rows of the ravine in one column of tiles.</summary>
public sealed class RavineSpan
{
    public int X { get; set; }
    public int Top { get; set; }
    public int Height { get; set; }
}

/// <summary>A pass through a mountain ridge, 2×2 tiles from its top-left tile; a rockfall fills it.</summary>
public sealed class MountainPass
{
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>The ridge runs east to west, so the pass leads north to south; otherwise the other way round.</summary>
    public bool Horizontal { get; set; }
}

/// <summary>
/// What stands in the walkers' way out in the wild land, generated once per village. The camp picture keeps the
/// ravine, the cave and the lookout. Hills and thickets stand farther out. The server owns this because citizens
/// clear a thicket or a rockfall, dig in the cave and bridge the ravine.
/// </summary>
public sealed class Wilds
{
    public List<RavineSpan> Ravine { get; set; } = [];
    public List<MountainPass> Passes { get; set; } = [];
    public List<Obstacle> Obstacles { get; set; } = [];

    /// <summary>Tile of the cave mouth, set into the south face of a ridge.</summary>
    public int CaveX { get; set; }

    public int CaveY { get; set; }

    /// <summary>Coal mouth. Black rock, its own tile, kept apart from the other mines.</summary>
    public int CoalX { get; set; }
    public int CoalY { get; set; }

    /// <summary>Iron mouth. Rust-coloured rock.</summary>
    public int IronX { get; set; }
    public int IronY { get; set; }

    /// <summary>Moonstone mouth. Pale rock. Chipping it still needs the waystone technology.</summary>
    public int MoonX { get; set; }
    public int MoonY { get; set; }

    /// <summary>Gold veins beside foe dens. Each one is dug on its own.</summary>
    public List<GoldDeposit> Gold { get; set; } = [];

    /// <summary>Tile at the top of the lookout peak; the trail up to it leaves from the tile below.</summary>
    public int LookoutX { get; set; }

    public int LookoutY { get; set; }

    public TilePoint CaveMouth => new(CaveX + 0.5, CaveY + 0.5);

    public TilePoint Lookout => new(LookoutX + 0.5, LookoutY + 0.5);
}

/// <summary>One gold vein beside a foe den. <see cref="Mined"/> counts gold already taken from this vein.</summary>
public sealed class GoldDeposit
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Level { get; set; }
    public int Mined { get; set; }

    public TilePoint Center => new(X + 0.5, Y + 0.5);

    public int Remaining => Math.Max(0, Rules.GoldVein.Size - Mined);
}

public static class Wilderness
{
    public const string Thicket = "thicket";
    public const string Rockfall = "rockfall";
    public const string Cave = "cave";
    public const string CoalMine = "coal-mine";
    public const string IronMine = "iron-mine";
    public const string MoonstoneMine = "moonstone-mine";

    /// <summary>Salt for the coal mouth. The stone cave uses no salt, so its tile stays put.</summary>
    public const uint CoalSalt = 0xC0A10001;
    public const uint IronSalt = 0xA00E0001;
    public const uint MoonstoneSalt = 0x90050001;

    /// <summary>How far, in tiles, a picked spot may be from the obstacle or the mine it is meant for.</summary>
    public const double SpotReach = 1.5;

    /// <summary>A cycle of clearing works on the obstacle tiles this close to the citizen's spot, nearest first.</summary>
    public const double PatchReach = 2.5;

    private const int RiverLeft = 30;
    private const int RiverRight = 33;

    /// <summary>Top row of the two-row road from the river to the east edge of the map; the web client keeps it bare.</summary>
    private const int RoadTop = 12;
    private const int CoreWidth = 44;
    private const int CoreHeight = 26;
    private const int RavineNorth = -22;
    private const int RavineSouth = -12;

    public static int WorkNeeded(ObstacleKind kind) => kind == ObstacleKind.Thicket ? 1 : 3;

    public static int WorkPerCycle(ObstacleKind kind) => kind == ObstacleKind.Thicket ? 3 : 2;

    public static ObstacleKind? ObstacleOf(TaskDefinition task) => task.Workplace switch
    {
        Thicket => ObstacleKind.Thicket,
        Rockfall => ObstacleKind.Rockfall,
        _ => null,
    };

    /// <summary>Tasks done out in the wild land at a place the server knows: an obstacle or a mine mouth.</summary>
    public static bool IsWild(TaskDefinition task) =>
        ObstacleOf(task) is not null || IsMine(task.Workplace) || task.Workplace == WorldMap.GoldVein;

    public static bool IsMine(string workplace) =>
        workplace is Cave or CoalMine or IronMine or MoonstoneMine;

    public static bool IsMouth(Wilds wilds, int x, int y) =>
        (x == wilds.CaveX && y == wilds.CaveY)
        || (x == wilds.CoalX && y == wilds.CoalY)
        || (x == wilds.IronX && y == wilds.IronY)
        || (x == wilds.MoonX && y == wilds.MoonY);

    public static TilePoint Mouth(Wilds wilds, string workplace) => workplace switch
    {
        CoalMine => new TilePoint(wilds.CoalX + 0.5, wilds.CoalY + 0.5),
        IronMine => new TilePoint(wilds.IronX + 0.5, wilds.IronY + 0.5),
        MoonstoneMine => new TilePoint(wilds.MoonX + 0.5, wilds.MoonY + 0.5),
        _ => wilds.CaveMouth,
    };

    public static string UnknownMine(string workplace) => workplace switch
    {
        CoalMine => "wilds.coalUnknown",
        IronMine => "wilds.ironUnknown",
        MoonstoneMine => "wilds.moonstoneUnknown",
        _ => "wilds.caveUnknown",
    };

    /// <summary>Places the coal, iron and moonstone mouths. The stone cave is <see cref="CaveSite.At"/> with no salt.</summary>
    public static void PlaceMines(Wilds wilds, uint seed)
    {
        var stone = CaveSite.At(seed);
        var coal = CaveSite.At(seed, CoalSalt, [stone]);
        var iron = CaveSite.At(seed, IronSalt, [stone, coal]);
        var moon = CaveSite.At(seed, MoonstoneSalt, [stone, coal, iron]);
        wilds.CoalX = coal.X;
        wilds.CoalY = coal.Y;
        wilds.IronX = iron.X;
        wilds.IronY = iron.Y;
        wilds.MoonX = moon.X;
        wilds.MoonY = moon.Y;
    }

    public static bool Stands(Obstacle obstacle) => obstacle.ClearedAt is null;

    /// <summary>
    /// Where a wild task is worked: at the cave mouth, or at the obstacle the player picked, else at the nearest one the
    /// tribe has seen. Throws a rule error when there is nothing to work there.
    /// </summary>
    public static TilePoint WorkSpot(Village village, TaskDefinition task, TilePoint? picked)
    {
        var wilds = village.Wilds ?? throw new GameRuleException("wilds.unknown", "Nobody knows the wild land yet.");
        if (task.Workplace == WorldMap.GoldVein)
        {
            var veins = wilds.Gold ?? [];
            var goldCamp = WorldMap.Places[WorldMap.Camp];
            if (picked is { } goldSpot)
            {
                var near = veins.MinBy(vein => vein.Center.DistanceTo(goldSpot));
                if (near is null || near.Center.DistanceTo(goldSpot) > SpotReach)
                {
                    throw new GameRuleException("spot.notForTask", $"\"{task.Name}\" is done at its own place.",
                        new Dictionary<string, object> { ["taskId"] = task.Id });
                }

                if (near.Remaining == 0)
                {
                    throw new GameRuleException("task.veinEmpty", "The gold vein is empty.",
                        new Dictionary<string, object> { ["taskId"] = task.Id });
                }

                return near.Center;
            }

            return veins.Where(vein => vein.Remaining > 0).MinBy(vein => vein.Center.DistanceTo(goldCamp))?.Center
                ?? throw new GameRuleException("task.veinEmpty", "The gold vein is empty.",
                    new Dictionary<string, object> { ["taskId"] = task.Id });
        }

        if (IsMine(task.Workplace))
        {
            var mouth = Mouth(wilds, task.Workplace);
            if (picked is { } spot && spot.DistanceTo(mouth) > SpotReach)
            {
                throw new GameRuleException("spot.notForTask", $"\"{task.Name}\" is done at its own place.",
                    new Dictionary<string, object> { ["taskId"] = task.Id });
            }

            var tile = mouth.ToTile();
            if (!village.IsExplored(tile))
            {
                throw new GameRuleException(UnknownMine(task.Workplace), "Nobody has found this mine yet.");
            }

            return mouth;
        }

        var kind = ObstacleOf(task) ?? throw new ArgumentException("Not a wild task.", nameof(task));
        if (picked is { } chosen)
        {
            Materialize(wilds, kind, chosen, Seed(village.Id.ToString()), SpotReach);
            var near = wilds.Obstacles.Where(o => o.Kind == kind && Stands(o) && o.Center.DistanceTo(chosen) <= SpotReach)
                .MinBy(o => o.Center.DistanceTo(chosen));
            return near?.Center ?? throw new GameRuleException("spot.nothingToClear", "Nothing is left to clear here.");
        }

        var standing = wilds.Obstacles.Where(o => o.Kind == kind && Stands(o)).ToList();

        var camp = WorldMap.Places[WorldMap.Camp];
        var nearest = standing.Where(o => village.IsExplored(o.Tile)).MinBy(o => o.Center.DistanceTo(camp));
        return nearest?.Center ?? (kind == ObstacleKind.Thicket
            ? throw new GameRuleException("wilds.noThicket", "Nobody has found a thicket yet. Explore the land first.")
            : throw new GameRuleException("wilds.noRockfall", "Nobody has found a rockfall yet. Explore the mountains first."));
    }

    /// <summary>Works away obstacle tiles around the spot, nearest first, up to <paramref name="work"/>. Returns the work done.</summary>
    public static int Clear(Village village, ObstacleKind kind, int work, TilePoint spot, DateTimeOffset now)
    {
        if (village.Wilds is { } wilds)
        {
            Materialize(wilds, kind, spot, Seed(village.Id.ToString()), PatchReach);
        }

        var done = 0;
        var near = (village.Wilds?.Obstacles ?? [])
            .Where(o => o.Kind == kind && Stands(o) && o.Center.DistanceTo(spot) <= PatchReach)
            .OrderBy(o => o.Center.DistanceTo(spot))
            .ThenBy(o => o.Y)
            .ThenBy(o => o.X);
        foreach (var obstacle in near)
        {
            if (done >= work)
            {
                break;
            }

            var share = Math.Min(work - done, WorkNeeded(kind) - obstacle.Work);
            obstacle.Work += share;
            done += share;
            if (obstacle.Work >= WorkNeeded(kind))
            {
                obstacle.ClearedAt = now;
            }
        }

        return done;
    }

    /// <summary>
    /// Records far thicket and rockfall tiles around a spot the first time someone works them.
    /// The camp picture is already stored. A tile that was cleared stays cleared.
    /// </summary>
    private static void Materialize(Wilds wilds, ObstacleKind kind, TilePoint spot, uint seed, double reach)
    {
        var known = wilds.Obstacles.Select(o => (o.X, o.Y)).ToHashSet();
        var wanted = kind == ObstacleKind.Thicket ? FarWilds.Kind.Thicket : FarWilds.Kind.Rockfall;
        var x0 = (int)Math.Floor(spot.X - reach);
        var x1 = (int)Math.Floor(spot.X + reach);
        var y0 = (int)Math.Floor(spot.Y - reach);
        var y1 = (int)Math.Floor(spot.Y + reach);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (known.Contains((x, y))) continue;
                if (new TilePoint(x + 0.5, y + 0.5).DistanceTo(spot) > reach) continue;
                if (FarWilds.At(x, y, seed) != wanted) continue;
                wilds.Obstacles.Add(new Obstacle { Kind = kind, X = x, Y = y });
                known.Add((x, y));
            }
        }
    }

    /// <summary>
    /// A rope bridge crosses the ravine when the ravine runs through every column of its footprint, between the top row
    /// (the north rim) and the bottom row (the south rim, its yard).
    /// </summary>
    public static bool Spans(Village village, TileArea footprint)
    {
        if (village.Wilds is not { } wilds || !WorldMap.Land.Contains(footprint))
        {
            return false;
        }

        for (var x = footprint.X; x < footprint.X + footprint.Width; x++)
        {
            var span = wilds.Ravine.FirstOrDefault(s => s.X == x);
            if (span is null || span.Top <= footprint.Y || span.Top + span.Height >= footprint.Y + footprint.Height)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The ravine, the cave and the lookout of a new village. Hills and thickets are not planted around the camp.</summary>
    public static Wilds Generate(Guid villageId)
    {
        var seed = Seed(villageId.ToString());
        var random = new Random(unchecked((int)seed));
        var wilds = new Wilds();
        var land = WorldMap.Vale;

        // The ravine cuts the land north of the camp from the west edge of the map to the river.
        var top = random.Next(-18, -13);
        for (var x = land.X; x < land.X + land.Width; x++)
        {
            if (x > land.X && random.NextDouble() < 0.35)
            {
                top = Math.Clamp(top + (random.Next(2) == 0 ? -1 : 1), RavineNorth, RavineSouth);
            }

            var rows = Enumerable.Range(top, 2).Where(y => x < RiverWestBank(y, seed)).ToList();
            if (rows.Count == 0)
            {
                break;
            }

            wilds.Ravine.Add(new RavineSpan { X = x, Top = rows[0], Height = rows.Count });
        }

        var ravine = wilds.Ravine.SelectMany(s => Enumerable.Range(s.Top, s.Height).Select(y => new TilePoint(s.X + 0.5, y + 0.5))).ToList();
        var mountains = new List<TilePoint>();
        var thickets = new List<TilePoint>();

        bool Fits(int x, int y, int campClearance, int riverClearance, double ravineClearance)
        {
            var point = new TilePoint(x + 0.5, y + 0.5);
            return x >= land.X + 4 && y >= land.Y + 4 && x < land.X + land.Width - 4 && y < land.Y + land.Height - 4
                   && !(x >= -campClearance && y >= -campClearance && x < CoreWidth + campClearance && y < CoreHeight + campClearance)
                   && Math.Abs(x + 0.5 - RiverCenter(y, seed)) > riverClearance
                   && !(x > RiverRight && y >= RoadTop - 1 && y <= RoadTop + 2)
                   && ravine.All(r => r.DistanceTo(point) > ravineClearance);
        }

        TilePoint? Pick(int campClearance, int riverClearance, double ravineClearance, double mountainSpacing, double thicketSpacing)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var x = random.Next(land.X, land.X + land.Width);
                var y = random.Next(land.Y, land.Y + land.Height);
                var point = new TilePoint(x, y);
                if (Fits(x, y, campClearance, riverClearance, ravineClearance)
                    && mountains.All(m => m.DistanceTo(point) > mountainSpacing)
                    && thickets.All(t => t.DistanceTo(point) > thicketSpacing))
                {
                    return point;
                }
            }

            return null;
        }

        // Mountain features keep away from each other, so each gets a ridge of its own.
        // This draw used to be the cave. It stays in the sequence so the lookout, the passes and the thickets
        // keep their places. The mouth itself stands outside the vale, at the seed's place.
        mountains.Add(Pick(10, 8, 6, 14, 0) ?? new TilePoint(-30, 20));
        var mouth = CaveSite.At(seed);
        wilds.CaveX = mouth.X;
        wilds.CaveY = mouth.Y;
        PlaceMines(wilds, seed);
        wilds.Gold = GoldVein.Place(seed);

        var lookout = Pick(10, 8, 6, 14, 0) ?? new TilePoint(-30, 40);
        mountains.Add(lookout);
        wilds.LookoutX = (int)lookout.X;
        wilds.LookoutY = (int)lookout.Y;

        return wilds;
    }

    /// <summary>
    /// Takes the thickets, rockfalls and passes off the camp picture. Returns true when something was removed.
    /// </summary>
    public static bool ClearAroundVillage(Village village)
    {
        if (village.Wilds is not { } wilds)
        {
            return false;
        }

        var removed = wilds.Obstacles.RemoveAll(obstacle =>
            obstacle.Kind is ObstacleKind.Thicket or ObstacleKind.Rockfall);
        var passes = wilds.Passes.Count;
        wilds.Passes.Clear();
        return removed > 0 || passes > 0;
    }

    /// <summary>Centre column of the river in a row: straight through the camp area, meandering beyond it. Mirrors the web client.</summary>
    public static double RiverCenter(int y, uint seed)
    {
        var straight = (RiverLeft + RiverRight) / 2.0;
        var distance = y < 0 ? -y : y >= CoreHeight ? y - CoreHeight + 1 : 0;
        if (distance == 0)
        {
            return straight;
        }

        var phase = seed % 1000 / 160.0;
        var sway = Math.Sin(distance * 0.11 + phase) * 7 + Math.Sin(distance * 0.27 + phase * 2) * 2;
        return straight + sway * Math.Min(1, distance / 10.0);
    }

    /// <summary>The westernmost water tile of the river in a row. Mirrors the web client.</summary>
    public static int RiverWestBank(int y, uint seed) => (int)Math.Floor(RiverCenter(y, seed) - 1.5 + 0.5);

    /// <summary>FNV-1a over the UTF-16 code units of the text, as the web client seeds the land from the village id.</summary>
    public static uint Seed(string text)
    {
        var value = 2166136261;
        foreach (var unit in text)
        {
            value ^= unit;
            value = unchecked(value * 16777619);
        }

        return value;
    }
}
