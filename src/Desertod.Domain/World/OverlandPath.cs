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
    private static readonly TileArea BridgeDeck = new(30, 12, 4, 2);

    /// <summary>Turns from after <paramref name="from"/> through to <paramref name="to"/>, or null when blocked.</summary>
    public static IReadOnlyList<TilePoint>? Find(Village village, TilePoint from, TilePoint to)
    {
        var terrain = AuthoredTerrain.For(village.Id, village.Wilds);
        var bridge = village.BridgeOpened && village.BridgeRuinedAt is null;
        var crossings = village.Structures
            .Where(s => s.Kind == BuildingKind.RopeBridge)
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
            if (kind is AuthoredTerrain.Thicket or AuthoredTerrain.Rockfall) return x == goalX && y == goalY;
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
        var goalX = (int)Math.Floor(to.X);
        var goalY = (int)Math.Floor(to.Y);
        bool Blocked(int x, int y) => FarBlocked(terrain, x, y, seed, wilds, goalX, goalY, bridge, crossings);
        if (goalX < land.X || goalY < land.Y || goalX >= land.X + land.Width || goalY >= land.Y + land.Height
            || Blocked(goalX, goalY))
        {
            return null;
        }

        var cursor = from;
        var points = new List<TilePoint>();
        for (var hop = 0; hop < 24; hop++)
        {
            var distance = cursor.DistanceTo(to);
            var steps = Math.Max(1, Math.Min(4000, (int)Math.Ceiling(distance)));
            int? hitX = null;
            int? hitY = null;
            for (var step = 1; step < steps; step++)
            {
                var x = (int)Math.Floor(cursor.X + (to.X - cursor.X) * step / steps);
                var y = (int)Math.Floor(cursor.Y + (to.Y - cursor.Y) * step / steps);
                if (!Blocked(x, y)) continue;
                hitX = x;
                hitY = y;
                break;
            }

            if (hitX is null || hitY is null)
            {
                points.Add(to);
                return points;
            }

            var stepped = false;
            foreach (var opening in FarWilds.Openings(hitX.Value, hitY.Value, Blocked))
            {
                var gate = new TilePoint(opening.X + 0.5, opening.Y + 0.5);
                var approach = Approach(cursor, opening.X, opening.Y, Blocked);
                if (Hits(cursor, approach, Blocked)) continue;
                if (gate.DistanceTo(approach) >= 0.01 && Hits(approach, gate, Blocked)) continue;
                if (approach.DistanceTo(cursor) < 0.01 && gate.DistanceTo(approach) < 0.01) continue;
                if (approach.DistanceTo(cursor) >= 0.01) points.Add(approach);
                if (gate.DistanceTo(approach) >= 0.01)
                {
                    points.Add(gate);
                    cursor = gate;
                }
                else
                {
                    cursor = approach;
                }

                stepped = true;
                break;
            }

            if (!stepped) return null;
        }

        return null;
    }

    /// <summary>The open tile beside a gap, on the side the walker is coming from.</summary>
    private static TilePoint Approach(TilePoint cursor, int openX, int openY, Func<int, int, bool> blocked)
    {
        var towardX = Math.Sign(cursor.X - (openX + 0.5));
        var towardY = Math.Sign(cursor.Y - (openY + 0.5));
        if (towardX != 0 && !blocked(openX + towardX, openY)) return new TilePoint(openX + towardX + 0.5, openY + 0.5);
        if (towardY != 0 && !blocked(openX, openY + towardY)) return new TilePoint(openX + 0.5, openY + towardY + 0.5);
        return new TilePoint(openX + 0.5, openY + 0.5);
    }

    /// <summary>Whether the straight step from <paramref name="a"/> to <paramref name="b"/> enters a blocked tile.</summary>
    private static bool Hits(TilePoint a, TilePoint b, Func<int, int, bool> blocked)
    {
        var steps = Math.Max(1, Math.Min(4000, (int)Math.Ceiling(a.DistanceTo(b))));
        for (var step = 1; step < steps; step++)
        {
            var x = (int)Math.Floor(a.X + (b.X - a.X) * step / steps);
            var y = (int)Math.Floor(a.Y + (b.Y - a.Y) * step / steps);
            if (blocked(x, y)) return true;
        }

        return false;
    }

    private static bool FarBlocked(
        byte[] terrain,
        int x,
        int y,
        uint seed,
        Wilds? wilds,
        int goalX,
        int goalY,
        bool bridge,
        IReadOnlyList<TileArea> crossings)
    {
        if (AuthoredTerrain.Inside(x, y))
        {
            var kind = terrain[AuthoredTerrain.Index(x, y)];
            if (x == goalX && y == goalY && kind is AuthoredTerrain.Thicket or AuthoredTerrain.Rockfall)
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

            if (kind is AuthoredTerrain.Mountain or AuthoredTerrain.Thicket or AuthoredTerrain.Rockfall)
            {
                return true;
            }

            return false;
        }

        if (wilds?.Obstacles.Any(o => o.X == x && o.Y == y && o.ClearedAt is not null) == true)
        {
            return false;
        }

        var far = FarWilds.At(x, y, seed);
        if (x == goalX && y == goalY && far is FarWilds.Kind.Thicket or FarWilds.Kind.Rockfall)
        {
            return false;
        }

        if (wilds is not null && Wilderness.IsMouth(wilds, x, y))
        {
            return false;
        }

        return FarWilds.Blocks(far) || FarGround.Water(x, y, seed);
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
        for (var guard = 0; (x != endX || y != endY) && guard < AuthoredTerrain.Width + AuthoredTerrain.Height; guard++)
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
