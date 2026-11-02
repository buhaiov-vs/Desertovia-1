using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public class FarWildsTests
{
    [Theory]
    [InlineData(1u, 2, 3, 4, 0.17061734478920698)]
    [InlineData(1u, 0, 0, 8, 0.4764070415403694)]
    [InlineData(7u, -5980, -4000, 0, 0.9320958030875772)]
    [InlineData(7u, 100, 200, 30, 0.7584631745703518)]
    public void Roll_MatchesTheBrowser(uint seed, int a, int b, int salt, double expected) =>
        Assert.Equal(expected, FarWilds.Roll(seed, a, b, salt), 12);

    [Fact]
    public void Vale_StaysOpen()
    {
        Assert.Equal(FarWilds.Kind.Open, FarWilds.At(0, 0, 1));
        Assert.False(FarGround.Water(0, 0, 1));
        for (var y = WorldMap.Vale.Y; y < WorldMap.Vale.Y + WorldMap.Vale.Height; y += 9)
        {
            for (var x = WorldMap.Vale.X; x < WorldMap.Vale.X + WorldMap.Vale.Width; x += 9)
            {
                Assert.Equal(FarWilds.Kind.Open, FarWilds.At(x, y, 1));
                Assert.False(FarGround.Water(x, y, 1));
            }
        }
    }

    [Fact]
    public void Hills_StandInEveryQuarterOfTheLand()
    {
        var land = WorldMap.Land;
        var span = land.Width / 2;
        (int X, int Y)[] corners = [(land.X, land.Y), (land.X + span, land.Y), (land.X, land.Y + span), (land.X + span, land.Y + span)];
        foreach (var (x0, y0) in corners)
        {
            var mountain = false;
            var thicket = false;
            for (var y = y0; y < y0 + span && (!mountain || !thicket); y += 4)
            {
                for (var x = x0; x < x0 + span && (!mountain || !thicket); x += 4)
                {
                    var kind = FarWilds.At(x, y, 1);
                    if (kind == FarWilds.Kind.Mountain) mountain = true;
                    if (kind == FarWilds.Kind.Thicket) thicket = true;
                }
            }

            Assert.True(mountain, $"mountain near {x0},{y0}");
            Assert.True(thicket, $"thicket near {x0},{y0}");
        }
    }

    [Fact]
    public void Courses_AreLongAndNotStraight()
    {
        var mountain = FindBig(1, kind => kind == FarWilds.Kind.Mountain, 400);
        var mountainSpread = Spread(mountain);
        Assert.True(mountain.Count > 400, $"mountain {mountain.Count}");
        Assert.True(mountainSpread.Width > 40 && mountainSpread.Height > 40, $"mountain {mountainSpread.Width}x{mountainSpread.Height}");

        var river = FindBig(1, kind => kind == FarWilds.Kind.Water, 150);
        var riverSpread = Spread(river);
        Assert.True(river.Count > 150, $"river {river.Count}");
        Assert.True(riverSpread.Width > 20 && riverSpread.Height > 20, $"river {riverSpread.Width}x{riverSpread.Height}");

        var thicket = FindBig(1, kind => kind == FarWilds.Kind.Thicket, 150);
        var thicketSpread = Spread(thicket);
        Assert.True(Math.Max(thicketSpread.Width, thicketSpread.Height) > 40, $"thicket {thicketSpread.Width}x{thicketSpread.Height}");
    }

    [Fact]
    public void Ranges_AreMostlyNarrow_AndSwellToBroadStretches()
    {
        static bool Rock(FarWilds.Kind kind) => kind == FarWilds.Kind.Mountain;
        bool Massif((int X, int Y) tile) => Solid(1, tile.X, tile.Y, 10, Rock);
        var ranges = AllBig(1, Rock, 400).Where(range => !range.Any(Massif)).Take(20).ToList();
        var tiles = ranges.Sum(range => range.Count);
        var narrow = ranges.Sum(range => range.Count(tile => !Solid(1, tile.X, tile.Y, 3, Rock)));
        Assert.True(narrow * 2 > tiles, $"narrow {narrow} of {tiles}");
        Assert.Contains(AllBig(1, Rock, 400), range => range.Any(tile => Solid(1, tile.X, tile.Y, 4, Rock) && !Massif(tile)));
    }

    [Fact]
    public void Massifs_StandAsSmallBroadMountainRegions()
    {
        static bool Rock(FarWilds.Kind kind) => kind == FarWilds.Kind.Mountain;
        var land = WorldMap.Land;
        var massifs = new List<(int X, int Y)>();
        var huge = 0;
        for (var y = land.Y + 20; y < land.Y + land.Height - 20; y += 8)
        {
            for (var x = land.X + 20; x < land.X + land.Width - 20; x += 8)
            {
                if (FarWilds.At(x, y, 1) != FarWilds.Kind.Mountain || !Solid(1, x, y, 6, Rock))
                {
                    continue;
                }

                if (Solid(1, x, y, 16, Rock))
                {
                    huge++;
                }

                if (!massifs.Any(m => Math.Abs(m.X - x) + Math.Abs(m.Y - y) < 400))
                {
                    massifs.Add((x, y));
                }
            }
        }

        Assert.True(massifs.Count >= 3, $"massifs {massifs.Count}");
        Assert.Equal(0, huge);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(7u)]
    public void EveryCrossing_IsAtLeastThreeTilesAcross(uint seed)
    {
        // Along sampled rows and columns, a stretch of mountain, thicket or water only one or two tiles across is the tip
        // or a corner of an obstacle, or the rim of a small hole sealed inside it: it never parts two wide stretches of land.
        var land = WorldMap.Land;
        bool Open(int x, int y) => FarWilds.At(x, y, seed) == FarWilds.Kind.Open;
        for (var line = land.Y + 1; line < land.Y + land.Height - 1; line += 13)
        {
            var run = -1;
            for (var x = land.X; x < land.X + land.Width; x++)
            {
                if (!Open(x, line))
                {
                    if (run >= 0) run++;
                    continue;
                }

                Assert.False(run is 1 or 2 && Parts(seed, (x - run - 1, line), (x, line)), $"seed {seed}: {run} tiles across at {x - run},{line}");
                run = 0;
            }
        }

        for (var line = land.X + 1; line < land.X + land.Width - 1; line += 13)
        {
            var run = -1;
            for (var y = land.Y; y < land.Y + land.Height; y++)
            {
                if (!Open(line, y))
                {
                    if (run >= 0) run++;
                    continue;
                }

                Assert.False(run is 1 or 2 && Parts(seed, (line, y - run - 1), (line, y)), $"seed {seed}: {run} tiles across at {line},{y - run}");
                run = 0;
            }
        }
    }

    [Fact]
    public void Mountain_StopsAWalker_UntilATunnelIsDugThrough()
    {
        const uint seed = 1;
        var terrain = new byte[AuthoredTerrain.Width * AuthoredTerrain.Height];
        var wilds = new Wilds();
        static TilePoint Centre(int x, int y) => new(x + 0.5, y + 0.5);

        // A narrow stretch of a range with no short way around it.
        var (fromX, y, toX) = NarrowCrossings(seed).First(crossing =>
            OverlandPath.Find(terrain, Centre(crossing.FromX, crossing.Y), Centre(crossing.ToX, crossing.Y), true, [], seed, wilds) is not { } way
            || Length(Centre(crossing.FromX, crossing.Y), way) > 20);
        var from = Centre(fromX, y);
        var to = Centre(toX, y);

        for (var x = fromX + 1; x < toX; x++)
        {
            wilds.Obstacles.Add(new Obstacle { Kind = ObstacleKind.Tunnel, X = x, Y = y, ClearedAt = DateTimeOffset.UnixEpoch });
        }

        var through = OverlandPath.Find(terrain, from, to, true, [], seed, wilds);

        Assert.NotNull(through);
        Assert.Equal([to], through);
    }

    [Fact]
    public void Fingerprint_MatchesTheBrowser()
    {
        var hash = 2166136261u;
        var land = WorldMap.Land;
        for (var y = land.Y; y < land.Y + land.Height; y += 80)
        {
            for (var x = land.X; x < land.X + land.Width; x += 80)
            {
                var kind = FarWilds.At(x, y, 1);
                var code = kind switch
                {
                    FarWilds.Kind.Open => 0,
                    FarWilds.Kind.Mountain => 1,
                    FarWilds.Kind.Thicket => 2,
                    _ => 4,
                };
                hash = (hash ^ (uint)code ^ (uint)((x & 255) << 8) ^ (uint)(y & 255)) * 16777619u;
            }
        }

        Assert.Equal(112541608u, hash);
    }

    /// <summary>Whether two open tiles lie in wide stretches of land that no short walk joins.</summary>
    private static bool Parts(uint seed, (int X, int Y) from, (int X, int Y) to) =>
        !Joined(seed, from, to) && !Joined(seed, from, null) && !Joined(seed, to, null);

    /// <summary>
    /// Whether a short walk over open ground leads from one open tile to the other; with no other tile, whether the open
    /// ground around the first is a small sealed hole.
    /// </summary>
    private static bool Joined(uint seed, (int X, int Y) from, (int X, int Y)? to)
    {
        var seen = new HashSet<(int, int)> { from };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(from);
        int[] dx = [1, 0, -1, 0];
        int[] dy = [0, -1, 0, 1];
        while (queue.Count > 0 && seen.Count < 400)
        {
            var here = queue.Dequeue();
            if (here == to) return true;
            if (to is null && queue.Count == 0 && seen.Count < 400) break;
            for (var dir = 0; dir < 4; dir++)
            {
                var next = (here.X + dx[dir], here.Y + dy[dir]);
                if (seen.Add(next) && FarWilds.At(next.Item1, next.Item2, seed) == FarWilds.Kind.Open) queue.Enqueue(next);
            }
        }

        return to is null && seen.Count < 400;
    }

    /// <summary>Rows where open ground, exactly three mountain tiles and open ground stand side by side.</summary>
    private static IEnumerable<(int FromX, int Y, int ToX)> NarrowCrossings(uint seed)
    {
        var land = WorldMap.Land;
        for (var y = land.Y + 20; y < land.Y + land.Height - 20; y += 3)
        {
            for (var x = land.X + 20; x < land.X + land.Width - 20; x++)
            {
                if (FarWilds.At(x, y, seed) == FarWilds.Kind.Open
                    && Enumerable.Range(1, 3).All(step => FarWilds.At(x + step, y, seed) == FarWilds.Kind.Mountain)
                    && FarWilds.At(x + 4, y, seed) == FarWilds.Kind.Open)
                {
                    yield return (x, y, x + 4);
                }
            }
        }
    }

    private static double Length(TilePoint from, IReadOnlyList<TilePoint> way)
    {
        var length = 0.0;
        var at = from;
        foreach (var turn in way)
        {
            length += at.DistanceTo(turn);
            at = turn;
        }

        return length;
    }

    private static bool Solid(uint seed, int x, int y, int half, Func<FarWilds.Kind, bool> accept)
    {
        for (var dy = -half; dy <= half; dy++)
        {
            for (var dx = -half; dx <= half; dx++)
            {
                if (!accept(FarWilds.At(x + dx, y + dy, seed))) return false;
            }
        }

        return true;
    }

    private static List<(int X, int Y)> FindBig(uint seed, Func<FarWilds.Kind, bool> accept, int least) =>
        AllBig(seed, accept, least).FirstOrDefault() ?? throw new InvalidOperationException("No long course on this seed.");

    private static IEnumerable<List<(int X, int Y)>> AllBig(uint seed, Func<FarWilds.Kind, bool> accept, int least)
    {
        var seen = new HashSet<(int, int)>();
        var land = WorldMap.Land;
        for (var y = land.Y + 20; y < land.Y + land.Height - 20; y += 16)
        {
            for (var x = land.X + 20; x < land.X + land.Width - 20; x += 16)
            {
                if (!seen.Add((x, y)) || !accept(FarWilds.At(x, y, seed)))
                {
                    continue;
                }

                var tiles = Component(seed, (x, y), accept);
                foreach (var tile in tiles)
                {
                    seen.Add(tile);
                }

                if (tiles.Count >= least)
                {
                    yield return tiles;
                }
            }
        }
    }

    private static (int X, int Y) Find(uint seed, Func<FarWilds.Kind, bool> accept)
    {
        var land = WorldMap.Land;
        for (var y = land.Y + 20; y < land.Y + land.Height - 20; y += 48)
        {
            for (var x = land.X + 20; x < land.X + land.Width - 20; x += 48)
            {
                if (accept(FarWilds.At(x, y, seed)))
                {
                    return (x, y);
                }
            }
        }

        throw new InvalidOperationException("No course on this seed.");
    }

    private static List<(int X, int Y)> Component(uint seed, (int X, int Y) start, Func<FarWilds.Kind, bool> accept)
    {
        var tiles = new List<(int X, int Y)>();
        var seen = new HashSet<(int, int)> { start };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(start);
        int[] dx = [1, 0, -1, 0];
        int[] dy = [0, -1, 0, 1];
        while (queue.Count > 0 && tiles.Count < 12_000)
        {
            var here = queue.Dequeue();
            tiles.Add(here);
            for (var dir = 0; dir < 4; dir++)
            {
                var next = (here.X + dx[dir], here.Y + dy[dir]);
                if (!seen.Add(next) || !accept(FarWilds.At(next.Item1, next.Item2, seed)))
                {
                    continue;
                }

                queue.Enqueue((next.Item1, next.Item2));
            }
        }

        return tiles;
    }

    private static (int Width, int Height) Spread(List<(int X, int Y)> tiles)
    {
        var minX = tiles.Min(tile => tile.X);
        var maxX = tiles.Max(tile => tile.X);
        var minY = tiles.Min(tile => tile.Y);
        var maxY = tiles.Max(tile => tile.Y);
        return (maxX - minX, maxY - minY);
    }
}
