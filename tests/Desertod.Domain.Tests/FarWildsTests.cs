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
        (int X, int Y)[] corners = [(land.X, land.Y), (land.X + land.Width - 750, land.Y), (land.X, land.Y + land.Height - 750), (land.X + land.Width - 750, land.Y + land.Height - 750)];
        foreach (var (x0, y0) in corners)
        {
            var mountain = false;
            var thicket = false;
            for (var y = y0; y < y0 + 750 && (!mountain || !thicket); y += 4)
            {
                for (var x = x0; x < x0 + 750 && (!mountain || !thicket); x += 4)
                {
                    var kind = FarWilds.At(x, y, 1);
                    if (kind is FarWilds.Kind.Mountain or FarWilds.Kind.Rockfall) mountain = true;
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
        var mountain = Component(1, Find(1, kind => kind is FarWilds.Kind.Mountain or FarWilds.Kind.Rockfall), kind => kind is FarWilds.Kind.Mountain or FarWilds.Kind.Rockfall);
        var mountainSpread = Spread(mountain);
        Assert.True(mountain.Count > 400, $"mountain {mountain.Count}");
        Assert.True(mountainSpread.Width > 40 && mountainSpread.Height > 40, $"mountain {mountainSpread.Width}x{mountainSpread.Height}");
        Assert.Contains(mountain, tile => FarWilds.At(tile.X, tile.Y, 1) == FarWilds.Kind.Rockfall);

        var river = Component(1, Find(1, kind => kind == FarWilds.Kind.Water), kind => kind == FarWilds.Kind.Water);
        var riverSpread = Spread(river);
        Assert.True(river.Count > 150, $"river {river.Count}");
        Assert.True(riverSpread.Width > 20 && riverSpread.Height > 20, $"river {riverSpread.Width}x{riverSpread.Height}");

        var thicket = FindBig(1, kind => kind == FarWilds.Kind.Thicket, 80);
        var thicketSpread = Spread(thicket);
        Assert.True(thicketSpread.Width > 15 && thicketSpread.Height > 15, $"thicket {thicketSpread.Width}x{thicketSpread.Height}");
    }

    [Fact]
    public void UnclearedRange_StopsAWalker_AndAClearedRockfallOpensIt()
    {
        var range = Component(1, Find(1, kind => kind is FarWilds.Kind.Mountain or FarWilds.Kind.Rockfall), kind => kind is FarWilds.Kind.Mountain or FarWilds.Kind.Rockfall);
        Assert.True(TryGate(1, range, out var fromX, out var fromY, out var toX, out var toY));
        var terrain = new byte[AuthoredTerrain.Width * AuthoredTerrain.Height];
        var from = new TilePoint(fromX + 0.5, fromY + 0.5);
        var to = new TilePoint(toX + 0.5, toY + 0.5);

        var wilds = new Wilds();
        var left = Math.Min(fromX, toX) - 2;
        var right = Math.Max(fromX, toX) + 2;
        var top = Math.Min(fromY, toY) - 2;
        var bottom = Math.Max(fromY, toY) + 2;
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                if (FarWilds.At(x, y, 1) != FarWilds.Kind.Rockfall)
                {
                    continue;
                }

                wilds.Obstacles.Add(new Obstacle
                {
                    Kind = ObstacleKind.Rockfall,
                    X = x,
                    Y = y,
                    ClearedAt = DateTimeOffset.UnixEpoch,
                });
            }
        }

        var way = OverlandPath.Find(terrain, from, to, false, [], 1, wilds);
        Assert.NotNull(way);
        Assert.Equal(to, way![^1]);
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
                    FarWilds.Kind.Rockfall => 3,
                    _ => 4,
                };
                hash = (hash ^ (uint)code ^ (uint)((x & 255) << 8) ^ (uint)(y & 255)) * 16777619u;
            }
        }

        Assert.Equal(257289047u, hash);
    }

    private static List<(int X, int Y)> FindBig(uint seed, Func<FarWilds.Kind, bool> accept, int least)
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
                    return tiles;
                }
            }
        }

        throw new InvalidOperationException("No long course on this seed.");
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

    private static bool TryGate(uint seed, List<(int X, int Y)> range, out int fromX, out int fromY, out int toX, out int toY)
    {
        (int X, int Y)[] axes = [(1, 0), (0, 1)];
        foreach (var (x, y) in range)
        {
            if (FarWilds.At(x, y, seed) != FarWilds.Kind.Rockfall)
            {
                continue;
            }

            foreach (var (dx, dy) in axes)
            {
                if (Span(seed, x, y, dx, dy, out fromX, out fromY, out toX, out toY))
                {
                    return true;
                }
            }
        }

        fromX = fromY = toX = toY = 0;
        return false;
    }

    private static bool Span(uint seed, int x, int y, int dx, int dy, out int fromX, out int fromY, out int toX, out int toY)
    {
        fromX = x;
        fromY = y;
        toX = x;
        toY = y;
        for (var step = 1; step <= 8; step++)
        {
            var kind = FarWilds.At(x - dx * step, y - dy * step, seed);
            if (kind == FarWilds.Kind.Rockfall)
            {
                continue;
            }

            if (kind != FarWilds.Kind.Open)
            {
                return false;
            }

            fromX = x - dx * step;
            fromY = y - dy * step;
            break;
        }

        for (var step = 1; step <= 8; step++)
        {
            var kind = FarWilds.At(x + dx * step, y + dy * step, seed);
            if (kind == FarWilds.Kind.Rockfall)
            {
                continue;
            }

            if (kind != FarWilds.Kind.Open)
            {
                return false;
            }

            toX = x + dx * step;
            toY = y + dy * step;
            break;
        }

        if (fromX == x && fromY == y)
        {
            return false;
        }

        if (toX == x && toY == y)
        {
            return false;
        }

        var across = Math.Abs(toX - fromX) + Math.Abs(toY - fromY);
        return across >= 2 && across <= 8;
    }
}
