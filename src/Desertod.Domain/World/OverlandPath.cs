using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;

namespace Desertod.Domain.World;

/// <summary>
/// Foot path between two points on the land, matching the browser <c>findWay</c>: around water, mountains and the
/// ravine, across a finished bridge deck or rope-bridge crossing. Marsh is avoided when a dry way exists.
/// </summary>
public static class OverlandPath
{
    private const double MarshCost = 3;
    private const double Diagonal = 1.4142135623730951;

    /// <summary>How many far tiles one walk may search before its way counts as blocked.</summary>
    private const int FarSearchLimit = 300_000;

    /// <summary>How strongly the far search leans toward the goal. Routes run at most this much longer than the shortest.</summary>
    private const double FarGreed = 1.2;
    private static readonly TileArea BridgeDeck = new(30, 12, 4, 2);

    /// <summary>Turns from after <paramref name="from"/> through to <paramref name="to"/>, or null when blocked.</summary>
    public static IReadOnlyList<TilePoint>? Find(Village village, TilePoint from, TilePoint to)
    {
        var terrain = AuthoredTerrain.For(village.Id, village.Wilds);
        var bridge = village.BridgeOpened && village.BridgeRuinedAt is null;
        var crossings = village.Structures
            .Where(s => s.Kind is BuildingKind.RopeBridge or BuildingKind.RiverBridge)
            .Select(s => WorldMap.Footprint(s.PlotId))
            .Where(a => a is not null)
            .Select(a => a!.Value)
            .ToList();
        return Find(terrain, from, to, bridge, crossings, Wilderness.Seed(village.Id.ToString()), village.Wilds);
    }

    public static IReadOnlyList<TilePoint>? Find(
        byte[] terrain,
        TilePoint from,
        TilePoint to,
        bool bridge,
        IReadOnlyList<TileArea> crossings,
        uint seed,
        Wilds? wilds = null)
    {
        var startX = (int)Math.Floor(from.X);
        var startY = (int)Math.Floor(from.Y);
        var goalX = (int)Math.Floor(to.X);
        var goalY = (int)Math.Floor(to.Y);
        if (!AuthoredTerrain.Inside(startX, startY) || !AuthoredTerrain.Inside(goalX, goalY))
        {
            return FarWay(terrain, from, to, bridge, crossings, seed, wilds);
        }

        bool Passable(int x, int y, bool climbing, bool bridging)
        {
            if (!AuthoredTerrain.Inside(x, y)) return false;
            if (x == startX && y == startY) return true;
            var kind = terrain[AuthoredTerrain.Index(x, y)];
            if (kind == AuthoredTerrain.Thicket) return x == goalX && y == goalY;
            if (kind == AuthoredTerrain.Water) return bridge && BridgeDeck.Contains(new(x, y, 1, 1));
            if (kind == AuthoredTerrain.Ravine) return bridging || crossings.Any(c => c.Contains(new(x, y, 1, 1)));
            return climbing || kind != AuthoredTerrain.Mountain;
        }

        bool Walkable(int x, int y) => Passable(x, y, false, false);
        double StepCost(int x, int y) => terrain[AuthoredTerrain.Index(x, y)] == AuthoredTerrain.Marsh ? MarshCost : 1;

        if (!Walkable(goalX, goalY)) return null;

        bool Dry(int x, int y) =>
            Walkable(x, y) && (StepCost(x, y) <= 1 || (x == startX && y == startY) || (x == goalX && y == goalY));

        if (Clear(from, to, Dry)) return [to];

        var start = AuthoredTerrain.Index(startX, startY);
        var goal = AuthoredTerrain.Index(goalX, goalY);
        var cameFrom = Search(start, goal, Walkable, StepCost);
        if (cameFrom is null) return null;

        var centres = new List<TilePoint>();
        for (var node = cameFrom[goal]; node != start && node != -1; node = cameFrom[node])
        {
            var x = node % AuthoredTerrain.Width + AuthoredTerrain.Left;
            var y = node / AuthoredTerrain.Width + AuthoredTerrain.Top;
            centres.Insert(0, new TilePoint(x + 0.5, y + 0.5));
        }

        var points = new List<TilePoint> { from };
        points.AddRange(centres);
        points.Add(to);

        var way = new List<TilePoint>();
        for (var at = 0; at < points.Count - 1;)
        {
            var next = points.Count - 1;
            while (next > at + 1 && !Clear(points[at], points[next], Dry)) next--;
            way.Add(points[next]);
            at = next;
        }

        return way;
    }

    private static IReadOnlyList<TilePoint>? FarWay(
        byte[] terrain,
        TilePoint from,
        TilePoint to,
        bool bridge,
        IReadOnlyList<TileArea> crossings,
        uint seed,
        Wilds? wilds)
    {
        var land = WorldMap.Land;
        var startX = (int)Math.Floor(from.X);
        var startY = (int)Math.Floor(from.Y);
        var goalX = (int)Math.Floor(to.X);
        var goalY = (int)Math.Floor(to.Y);
        var cleared = wilds?.Obstacles.Where(o => o.ClearedAt is not null).Select(o => (o.X, o.Y)).ToHashSet();
        bool Blocked(int x, int y) => FarBlocked(terrain, x, y, seed, wilds, cleared, goalX, goalY, bridge, crossings);
        if (goalX < land.X || goalY < land.Y || goalX >= land.X + land.Width || goalY >= land.Y + land.Height
            || Blocked(goalX, goalY))
        {
            return null;
        }

        bool Open(int x, int y) =>
            (x == startX && y == startY)
            || (x >= land.X && y >= land.Y && x < land.X + land.Width && y < land.Y + land.Height && !Blocked(x, y));

        if (Clear(from, to, Open)) return [to];

        var tiles = FarSearch(startX, startY, goalX, goalY, Open);
        if (tiles is null) return null;

        var points = new List<TilePoint> { from };
        points.AddRange(tiles.Skip(1).SkipLast(1).Select(tile => new TilePoint(tile.X + 0.5, tile.Y + 0.5)));
        points.Add(to);

        return Pull(points, Open);
    }

    /// <summary>
    /// Tile centres from the start tile to the goal tile, found by a slightly greedy A*: routes run at most about a fifth
    /// longer than the shortest, and long walks stay quick. Null when no way is found within <see cref="FarSearchLimit"/> tiles.
    /// Matches <c>farSearch</c> in the browser step for step, so both agree on what can be reached.
    /// </summary>
    private static List<(int X, int Y)>? FarSearch(int startX, int startY, int goalX, int goalY, Func<int, int, bool> open)
    {
        var cost = new Dictionary<int, double>();
        var cameFrom = new Dictionary<int, int>();
        var closed = new HashSet<int>();
        var heap = new List<(int Key, double Score, double Cost)>();

        double Heuristic(int x, int y)
        {
            var dx = Math.Abs(x - goalX);
            var dy = Math.Abs(y - goalY);

            return FarGreed * (Math.Max(dx, dy) + (Diagonal - 1) * Math.Min(dx, dy));
        }

        // Lower score first; on a tie the node further along, then the lower key, so both sides pop in the same order.
        bool Before((int Key, double Score, double Cost) a, (int Key, double Score, double Cost) b) =>
            a.Score < b.Score || (a.Score == b.Score && (a.Cost > b.Cost || (a.Cost == b.Cost && a.Key < b.Key)));

        void Push(int key, double score, double reached)
        {
            heap.Add((key, score, reached));
            var child = heap.Count - 1;
            while (child > 0)
            {
                var parent = (child - 1) >> 1;
                if (!Before(heap[child], heap[parent])) break;
                (heap[parent], heap[child]) = (heap[child], heap[parent]);
                child = parent;
            }
        }

        (int Key, double Score, double Cost) Pop()
        {
            var top = heap[0];
            var last = heap[^1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count > 0)
            {
                heap[0] = last;
                var parent = 0;
                for (;;)
                {
                    var left = parent * 2 + 1;
                    var right = left + 1;
                    var first = parent;
                    if (left < heap.Count && Before(heap[left], heap[first])) first = left;
                    if (right < heap.Count && Before(heap[right], heap[first])) first = right;
                    if (first == parent) break;
                    (heap[parent], heap[first]) = (heap[first], heap[parent]);
                    parent = first;
                }
            }

            return top;
        }

        var start = TileKey(startX, startY);
        var goal = TileKey(goalX, goalY);
        cost[start] = 0;
        Push(start, Heuristic(startX, startY), 0);
        while (heap.Count > 0)
        {
            var (node, _, reached) = Pop();
            if (!closed.Add(node)) continue;
            if (node == goal) break;
            if (closed.Count > FarSearchLimit) return null;

            var (x, y) = FromKey(node);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 || !open(x + dx, y + dy)) continue;
                    if (dx != 0 && dy != 0 && (!open(x + dx, y) || !open(x, y + dy))) continue;
                    var next = TileKey(x + dx, y + dy);
                    var nextCost = reached + (dx != 0 && dy != 0 ? Diagonal : 1);
                    if (closed.Contains(next) || (cost.TryGetValue(next, out var known) && nextCost >= known)) continue;
                    cost[next] = nextCost;
                    cameFrom[next] = node;
                    Push(next, nextCost + Heuristic(x + dx, y + dy), nextCost);
                }
            }
        }

        if (!closed.Contains(goal)) return null;

        var tiles = new List<(int X, int Y)>();
        for (var node = goal; ; node = cameFrom[node])
        {
            tiles.Add(FromKey(node));
            if (node == start) break;
        }

        tiles.Reverse();

        return tiles;
    }

    /// <summary>
    /// Keeps only the turns of a route: from each kept point, the furthest later point still in a straight clear line.
    /// The reach doubles while the line stays clear, then halves back, so a long route is trimmed quickly.
    /// </summary>
    private static List<TilePoint> Pull(List<TilePoint> points, Func<int, int, bool> open)
    {
        var way = new List<TilePoint>();
        for (var at = 0; at < points.Count - 1;)
        {
            var good = at + 1;
            var reach = 1;
            while (good + reach < points.Count && Clear(points[at], points[good + reach], open))
            {
                good += reach;
                reach *= 2;
            }

            for (reach /= 2; reach > 0; reach /= 2)
            {
                if (good + reach < points.Count && Clear(points[at], points[good + reach], open)) good += reach;
            }

            way.Add(points[good]);
            at = good;
        }

        return way;
    }

    /// <summary>Signed 32-bit pack of a tile. Matches <c>tileKey</c> in the browser.</summary>
    private static int TileKey(int x, int y) => unchecked(((x + 8_000) << 16) | ((y + 8_000) & 0xffff));

    private static (int X, int Y) FromKey(int key) => ((key >> 16) - 8_000, (key & 0xffff) - 8_000);

    private static bool FarBlocked(
        byte[] terrain,
        int x,
        int y,
        uint seed,
        Wilds? wilds,
        HashSet<(int X, int Y)>? cleared,
        int goalX,
        int goalY,
        bool bridge,
        IReadOnlyList<TileArea> crossings)
    {
        if (AuthoredTerrain.Inside(x, y))
        {
            var kind = terrain[AuthoredTerrain.Index(x, y)];
            if (x == goalX && y == goalY && kind == AuthoredTerrain.Thicket)
            {
                return false;
            }

            if (kind == AuthoredTerrain.Water)
            {
                return !(bridge && BridgeDeck.Contains(new TileArea(x, y, 1, 1)));
            }

            if (kind == AuthoredTerrain.Ravine)
            {
                return !crossings.Any(crossing => crossing.Contains(new TileArea(x, y, 1, 1)));
            }

            if (kind is AuthoredTerrain.Mountain or AuthoredTerrain.Thicket)
            {
                return true;
            }

            return false;
        }

        if (cleared?.Contains((x, y)) == true)
        {
            return false;
        }

        var far = FarWilds.At(x, y, seed);
        // A walker may step up to the thicket they clear or the mountain face they dig.
        if (x == goalX && y == goalY && far is FarWilds.Kind.Thicket or FarWilds.Kind.Mountain)
        {
            return false;
        }

        if (wilds is not null && Wilderness.IsMouth(wilds, x, y))
        {
            return false;
        }

        // A finished river bridge carries walkers over the water under its deck.
        if (far == FarWilds.Kind.Water && crossings.Any(crossing => crossing.Contains(new TileArea(x, y, 1, 1))))
        {
            return false;
        }

        return FarWilds.Blocks(far) || far == FarWilds.Kind.Water;
    }

    private static bool Clear(TilePoint a, TilePoint b, Func<int, int, bool> dry)
    {
        var x = (int)Math.Floor(a.X);
        var y = (int)Math.Floor(a.Y);
        var endX = (int)Math.Floor(b.X);
        var endY = (int)Math.Floor(b.Y);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var stepX = Math.Sign(dx);
        var stepY = Math.Sign(dy);
        var tDeltaX = dx == 0 ? double.PositiveInfinity : Math.Abs(1 / dx);
        var tDeltaY = dy == 0 ? double.PositiveInfinity : Math.Abs(1 / dy);
        var tMaxX = dx == 0 ? double.PositiveInfinity : (stepX > 0 ? x + 1 - a.X : a.X - x) * tDeltaX;
        var tMaxY = dy == 0 ? double.PositiveInfinity : (stepY > 0 ? y + 1 - a.Y : a.Y - y) * tDeltaY;
        if (!dry(x, y)) return false;
        var steps = Math.Abs(endX - x) + Math.Abs(endY - y) + 2;
        for (var guard = 0; (x != endX || y != endY) && guard < steps; guard++)
        {
            if (tMaxX < tMaxY)
            {
                x += stepX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxX)
            {
                y += stepY;
                tMaxY += tDeltaY;
            }
            else
            {
                if (!dry(x + stepX, y) || !dry(x, y + stepY)) return false;
                x += stepX;
                y += stepY;
                tMaxX += tDeltaX;
                tMaxY += tDeltaY;
            }

            if (!dry(x, y)) return false;
        }

        return x == endX && y == endY;
    }

    private static int[]? Search(int start, int goal, Func<int, int, bool> walkable, Func<int, int, double> stepCost)
    {
        var width = AuthoredTerrain.Width;
        var height = AuthoredTerrain.Height;
        var goalX = goal % width + AuthoredTerrain.Left;
        var goalY = goal / width + AuthoredTerrain.Top;
        var cost = new double[width * height];
        Array.Fill(cost, double.PositiveInfinity);
        var cameFrom = new int[width * height];
        Array.Fill(cameFrom, -1);
        var closed = new byte[width * height];
        var open = new List<(int Node, double Score)>();

        double Heuristic(int x, int y)
        {
            var dx = Math.Abs(x - goalX);
            var dy = Math.Abs(y - goalY);
            return Math.Max(dx, dy) + (Math.Sqrt(2) - 1) * Math.Min(dx, dy);
        }

        void Push(int node, double score)
        {
            open.Add((node, score));
            var child = open.Count - 1;
            while (child > 0)
            {
                var parent = (child - 1) >> 1;
                if (open[parent].Score <= open[child].Score) break;
                (open[parent], open[child]) = (open[child], open[parent]);
                child = parent;
            }
        }

        int Pop()
        {
            var top = open[0];
            var last = open[^1];
            open.RemoveAt(open.Count - 1);
            if (open.Count > 0)
            {
                open[0] = last;
                var parent = 0;
                for (;;)
                {
                    var left = parent * 2 + 1;
                    var right = left + 1;
                    var smallest = parent;
                    if (left < open.Count && open[left].Score < open[smallest].Score) smallest = left;
                    if (right < open.Count && open[right].Score < open[smallest].Score) smallest = right;
                    if (smallest == parent) break;
                    (open[parent], open[smallest]) = (open[smallest], open[parent]);
                    parent = smallest;
                }
            }

            return top.Node;
        }

        cost[start] = 0;
        Push(start, Heuristic(start % width + AuthoredTerrain.Left, start / width + AuthoredTerrain.Top));
        while (open.Count > 0 && closed[goal] == 0)
        {
            var node = Pop();
            if (closed[node] > 0) continue;
            closed[node] = 1;
            var x = node % width + AuthoredTerrain.Left;
            var y = node / width + AuthoredTerrain.Top;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 || !walkable(x + dx, y + dy)) continue;
                    if (dx != 0 && dy != 0 && (!walkable(x + dx, y) || !walkable(x, y + dy))) continue;
                    var next = AuthoredTerrain.Index(x + dx, y + dy);
                    var nextCost = cost[node] + (dx != 0 && dy != 0 ? Math.Sqrt(2) : 1) * stepCost(x + dx, y + dy);
                    if (closed[next] > 0 || nextCost >= cost[next]) continue;
                    cost[next] = nextCost;
                    cameFrom[next] = node;
                    Push(next, nextCost + Heuristic(x + dx, y + dy));
                }
            }
        }

        return closed[goal] > 0 ? cameFrom : null;
    }
}
