namespace Desertod.Domain.World;

/// <summary>
/// Mountains, thickets and rivers outside the camp picture.
/// A ridge keeps a general heading, bends a little as it goes, and splits off shorter spurs.
/// Thickets are clumps along that same growth. The web client's farWilds.ts computes the same tiles.
/// The camp picture stays open, and each course keeps a stretch of open country around the camp.
/// One ridge and one thicket stand in every square of the land.
/// </summary>
public static class FarWilds
{
    public const int OpeningReach = 240;

    private const int RiverCourses = 8;
    private const int Region = 750;
    private const int Regions = WorldMap.Side / Region;

    private static readonly int[] Dx = [1, 0, -1, 0];
    private static readonly int[] Dy = [0, -1, 0, 1];
    private static readonly int[] Dx8 = [1, 1, 0, -1, -1, -1, 0, 1];
    private static readonly int[] Dy8 = [0, -1, -1, -1, 0, 1, 1, 1];
    private static readonly int[] Turn = [0, 1, 7, 2, 6, 3, 5, 4];

    private static readonly Dictionary<uint, Dictionary<int, Kind>> Cache = new();
    private static readonly object CacheLock = new();

    public enum Kind
    {
        Open,
        Mountain,
        Thicket,
        Rockfall,
        Water,
    }

    /// <summary>Stable 0…1 roll. Matches <c>farWildRoll</c>.</summary>
    public static double Roll(uint seed, int a, int b, int salt)
    {
        unchecked
        {
            var value = seed ^ (uint)(a * 0x27d4eb2d) ^ (uint)(b * 0x165667b1) ^ (uint)(salt * (int)0x9E3779B1);
            value = (uint)((int)(value ^ (value >> 16)) * (int)0x85EBCA6B);
            value = (uint)((int)(value ^ (value >> 13)) * (int)0xC2B2AE35);
            value ^= value >> 16;
            return value / 4_294_967_296.0;
        }
    }

    public static Kind At(int x, int y, uint seed)
    {
        if (!OnLand(x, y) || InVale(x, y))
        {
            return Kind.Open;
        }

        return Courses(seed).TryGetValue(Key(x, y), out var kind) ? kind : Kind.Open;
    }

    /// <summary>A mountain, thicket or rockfall blocks a walk until it is cleared. A river is water, not a clearing.</summary>
    public static bool Blocks(Kind kind) => kind is Kind.Mountain or Kind.Thicket or Kind.Rockfall;

    public static bool Blocks(int x, int y, uint seed) => Blocks(At(x, y, seed));

    /// <summary>
    /// The nearest open tile at a gap or at the end of the obstacle, within <see cref="OpeningReach"/> steps along it.
    /// Grass beside a solid stretch is not a gap. A cleared rockfall, and the end of a river, are.
    /// </summary>
    public static (int X, int Y)? Opening(int x, int y, Func<int, int, bool> blocked, int reach = OpeningReach)
    {
        foreach (var gap in Openings(x, y, blocked, reach))
        {
            return gap;
        }

        return null;
    }

    /// <summary>Gaps and ends of the obstacle, nearest first. A walk tries the next one when the nearest still crosses the wall.</summary>
    public static IReadOnlyList<(int X, int Y)> Openings(int x, int y, Func<int, int, bool> blocked, int reach = OpeningReach)
    {
        if (!blocked(x, y))
        {
            return [];
        }

        var queue = new List<(int X, int Y, int Step)> { (x, y, 0) };
        var seen = new HashSet<int> { Key(x, y) };
        var minX = queue[0];
        var maxX = queue[0];
        var minY = queue[0];
        var maxY = queue[0];
        var passes = new List<(int X, int Y, int Step)>();
        for (var head = 0; head < queue.Count && head < reach; head++)
        {
            var here = queue[head];
            if (here.X < minX.X || (here.X == minX.X && here.Step < minX.Step)) minX = here;
            if (here.X > maxX.X || (here.X == maxX.X && here.Step < maxX.Step)) maxX = here;
            if (here.Y < minY.Y || (here.Y == minY.Y && here.Step < minY.Step)) minY = here;
            if (here.Y > maxY.Y || (here.Y == maxY.Y && here.Step < maxY.Step)) maxY = here;
            for (var dir = 0; dir < 4; dir++)
            {
                var nx = here.X + Dx[dir];
                var ny = here.Y + Dy[dir];
                if (blocked(nx, ny))
                {
                    if (seen.Add(Key(nx, ny)))
                    {
                        queue.Add((nx, ny, here.Step + 1));
                    }

                    continue;
                }

                var across = dir % 2 == 0
                    ? blocked(nx, ny - 1) && blocked(nx, ny + 1)
                    : blocked(nx - 1, ny) && blocked(nx + 1, ny);
                if (across)
                {
                    passes.Add((nx, ny, here.Step + 1));
                }
            }
        }

        var found = new List<(int X, int Y, int Step)>();
        var gaps = new HashSet<int>();
        void Offer(int gx, int gy, int step)
        {
            if (blocked(gx, gy) || !gaps.Add(Key(gx, gy)))
            {
                return;
            }

            found.Add((gx, gy, step));
        }

        var tips = new List<(int X, int Y, int Step)>();
        foreach (var here in queue)
        {
            var touch = 0;
            for (var dir = 0; dir < 4; dir++)
            {
                if (blocked(here.X + Dx[dir], here.Y + Dy[dir]))
                {
                    touch++;
                }
            }

            if (touch <= 1)
            {
                tips.Add(here);
            }
        }

        var ends = tips.Count > 0 ? tips : [minX, maxX, minY, maxY];
        foreach (var tip in ends)
        {
            for (var dir = 0; dir < 4; dir++)
            {
                Offer(tip.X + Dx[dir], tip.Y + Dy[dir], tip.Step + 1);
            }
        }

        foreach (var pass in passes)
        {
            Offer(pass.X, pass.Y, pass.Step);
        }

        found.Sort((a, b) => a.Step.CompareTo(b.Step));
        return found.Select(gap => (gap.X, gap.Y)).ToList();
    }

    /// <summary>Signed 32-bit pack of a tile. Matches <c>tileKey</c>.</summary>
    private static int Key(int x, int y)
    {
        unchecked
        {
            return ((x + 8_000) << 16) | ((y + 8_000) & 0xffff);
        }
    }

    private static Dictionary<int, Kind> Courses(uint seed)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(seed, out var map))
            {
                return map;
            }

            map = Build(seed);
            Cache[seed] = map;
            return map;
        }
    }

    private static Dictionary<int, Kind> Build(uint seed)
    {
        var map = new Dictionary<int, Kind>();

        void Paint(int x, int y, Kind kind)
        {
            if (!OnLand(x, y) || InVale(x, y))
            {
                return;
            }

            var key = Key(x, y);
            if (map.TryGetValue(key, out var prev))
            {
                if (prev == Kind.Rockfall)
                {
                    return;
                }

                if (prev == Kind.Mountain && kind != Kind.Rockfall)
                {
                    return;
                }

                if (kind == Kind.Thicket)
                {
                    return;
                }

                if (kind == Kind.Water)
                {
                    return;
                }
            }

            map[key] = kind;
        }

        int CountryReach(int id, int salt) => 120 + (int)Math.Floor(Roll(seed, id, salt, 91) * 160);

        bool ClearOfCamp(int x, int y, int reach)
        {
            var dx = x - 15;
            var dy = y - 13;
            return dx * dx + dy * dy >= reach * reach && !InVale(x, y);
        }

        (int X, int Y)? Start(int id, int salt)
        {
            var span = WorldMap.Side - 400;
            var x = WorldMap.Land.X + 200 + (int)Math.Floor(Roll(seed, id, salt, 1) * span);
            var y = WorldMap.Land.Y + 200 + (int)Math.Floor(Roll(seed, id, salt, 2) * span);
            if (!ClearOfCamp(x, y, CountryReach(id, salt)))
            {
                return null;
            }

            return (x, y);
        }

        (int X, int Y)? Cell(int index, int salt)
        {
            var column = index % Regions;
            var row = index / Regions;
            var x = WorldMap.Land.X + column * Region + 30 + (int)Math.Floor(Roll(seed, index, salt, 1) * (Region - 60));
            var y = WorldMap.Land.Y + row * Region + 30 + (int)Math.Floor(Roll(seed, index, salt, 2) * (Region - 60));
            if (!ClearOfCamp(x, y, CountryReach(index, salt)))
            {
                return null;
            }

            return (x, y);
        }

        void Grow(int id, int salt, int x, int y, int length, Kind kind, int depth, int dir, int home, int reach, double wobble, int forkEvery, double forkChance, int maxDepth, int childMin, int childSpan)
        {
            var px = x;
            var py = y;
            for (var step = 0; step < length; step++)
            {
                if (!OnLand(px, py) || !ClearOfCamp(px, py, reach))
                {
                    break;
                }

                var bulk = kind == Kind.Water
                    ? Roll(seed, id, step, salt + 61) < 0.18 ? 1 : 0
                    : kind == Kind.Thicket
                        ? depth == 0 ? 2 : 1
                        : depth == 0 ? 1 + (Roll(seed, px, py, salt + 60) < 0.4 ? 1 : 0) : depth == 1 ? 1 : 0;
                var skip = kind == Kind.Thicket ? 0.34 : 0;
                var gateEvery = 36 + (int)Math.Floor(Roll(seed, id, salt, 4) * 12);
                var gating = kind == Kind.Mountain && depth == 0 && step > 6 && step % gateEvery == 0;
                if (gating)
                {
                    Paint(px, py, Kind.Rockfall);
                    for (var spoke = 0; spoke < 8; spoke += 2)
                    {
                        for (var n = 1; n <= bulk + 2; n++)
                        {
                            Paint(px + Dx8[spoke] * n, py + Dy8[spoke] * n, Kind.Rockfall);
                        }
                    }
                }

                for (var oy = -bulk; oy <= bulk; oy++)
                {
                    for (var ox = -bulk; ox <= bulk; ox++)
                    {
                        if (ox * ox + oy * oy > bulk * bulk)
                        {
                            continue;
                        }

                        if (skip > 0 && Roll(seed, px + ox, py + oy, salt + 80) < skip)
                        {
                            continue;
                        }

                        Paint(px + ox, py + oy, gating ? Kind.Rockfall : kind);
                    }
                }

                if (depth < maxDepth && step > 5 && step % forkEvery == 0 && Roll(seed, id, step, salt + 70) < forkChance)
                {
                    var off = Roll(seed, id, step, salt + 71) < 0.5 ? 2 : 6;
                    var childDir = (dir + off) % 8;
                    var childLen = childMin + (int)Math.Floor(Roll(seed, id, step, salt + 73) * childSpan);
                    var childId = unchecked(id + (step + 1) * 1024 + (depth + 1) * 262144);
                    Grow(childId, salt, px, py, childLen, kind, depth + 1, childDir, childDir, reach, wobble, forkEvery, forkChance * 0.45, maxDepth, Math.Max(8, childMin >> 1), Math.Max(8, childSpan >> 1));
                }

                var drift = (dir - home + 8) % 8;
                var sway = Roll(seed, id, step, salt + 50);
                if (drift >= 3 && drift <= 5 && sway < 0.75)
                {
                    dir = (dir + (drift <= 4 ? 7 : 1)) % 8;
                }
                else if (sway < wobble)
                {
                    var side = Roll(seed, id, step, salt + 51) < 0.5 ? 1 : 7;
                    dir = (dir + side) % 8;
                }

                var moved = false;
                for (var turn = 0; turn < 8; turn++)
                {
                    var nd = (dir + Turn[turn]) % 8;
                    var nx = px + Dx8[nd];
                    var ny = py + Dy8[nd];
                    if (!OnLand(nx, ny) || !ClearOfCamp(nx, ny, reach))
                    {
                        continue;
                    }

                    dir = nd;
                    px = nx;
                    py = ny;
                    if (Dx8[nd] != 0 && Dy8[nd] != 0)
                    {
                        Paint(px - Dx8[nd], py, gating ? Kind.Rockfall : kind);
                    }

                    moved = true;
                    break;
                }

                if (!moved)
                {
                    break;
                }
            }
        }

        var squares = Regions * Regions;
        for (var index = 0; index < squares; index++)
        {
            if (Cell(index, 70) is not { } origin)
            {
                continue;
            }

            var length = 78 + (int)Math.Floor(Roll(seed, index, 70, 5) * 36);
            var dir = (int)Math.Floor(Roll(seed, index, 70, 3) * 8);
            Grow(index, 70, origin.X, origin.Y, length, Kind.Mountain, 0, dir, dir, CountryReach(index, 70), 0.55, 8, 0.9, 2, 18, 26);
        }

        for (var index = 0; index < RiverCourses; index++)
        {
            if (Start(index, 110) is not { } origin)
            {
                continue;
            }

            var radius = 7 + (int)Math.Floor(Roll(seed, index, 110, 6) * 4);
            var reach = CountryReach(index, 110);
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= radius * radius && ClearOfCamp(origin.X + dx, origin.Y + dy, reach))
                    {
                        Paint(origin.X + dx, origin.Y + dy, Kind.Water);
                    }
                }
            }

            var toEdge = Roll(seed, index, 110, 7) < 0.5;
            var length = toEdge ? 1400 : 420 + (int)Math.Floor(Roll(seed, index, 110, 8) * 380);
            var dir = (int)Math.Floor(Roll(seed, index, 110, 3) * 8);
            Grow(index, 110, origin.X, origin.Y, length, Kind.Water, 0, dir, dir, CountryReach(index, 110), 0.42, 90, 0.55, 1, 36, 40);
        }

        for (var index = 0; index < squares; index++)
        {
            if (Cell(index, 90) is not { } origin)
            {
                continue;
            }

            var length = 28 + (int)Math.Floor(Roll(seed, index, 90, 5) * 18);
            var dir = (int)Math.Floor(Roll(seed, index, 90, 3) * 8);
            Grow(index, 90, origin.X, origin.Y, length, Kind.Thicket, 0, dir, dir, CountryReach(index, 90), 0.7, 6, 0.85, 2, 10, 12);
        }

        return map;
    }

    private static bool OnLand(int x, int y)
    {
        var land = WorldMap.Land;
        return x >= land.X && y >= land.Y && x < land.X + land.Width && y < land.Y + land.Height;
    }

    private static bool InVale(int x, int y) => WorldMap.Vale.Contains(new TileArea(x, y, 1, 1));
}
