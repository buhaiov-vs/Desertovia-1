namespace Desertod.Domain.World;

/// <summary>
/// Mountains, thickets and rivers outside the camp picture.
/// A course keeps a general heading, bends a little as it goes, and splits off thinner branches.
/// Most of its length is one tile wide, and here and there it swells gradually to a broad stretch.
/// A few mountain regions stand as solid massifs, with ranges running out of them.
/// Every other pass across a range is an open saddle and the rest are rockfall to clear. A massif is cut by
/// alternating open valleys and rockfall lines, and rivers and thickets have open gaps, so the whole land can be walked.
/// The web client's farWilds.ts computes the same tiles.
/// The camp picture stays open, and each course keeps a stretch of open country around the camp.
/// </summary>
public static class FarWilds
{
    /// <summary>How many obstacle tiles a walk looks through for a gap.</summary>
    public const int OpeningReach = 1200;

    private const int RiverCourses = 12;
    private const int Massifs = 8;

    /// <summary>Ranges grow one per square of this size.</summary>
    private const int RangeRegion = 3000;

    /// <summary>Thickets grow in most of the squares of this size.</summary>
    private const int ThicketRegion = 1000;

    /// <summary>Valleys and rockfall passes cut a massif along lines this far apart.</summary>
    private const int MassifPassSpacing = 40;

    /// <summary>A course cuts a pass, ford or gap once this much of its cross-section has grown since the last one.</summary>
    private const double GateLoad = 400;

    /// <summary>A course stops once it heads this close to the edge of the land.</summary>
    private const int EdgeStop = 8;

    /// <summary>An open saddle, valley or ford. It reads as open ground, and no later course paints over it.</summary>
    private const Kind Saddle = (Kind)byte.MaxValue;

    private static readonly Shape Range = new(0.15, 80, 0.6, 2, 150, 250, [7.5, 3.5, 1.5], 150, 0.4, 0);
    private static readonly Shape Thicket = new(0.3, 40, 0.6, 1, 50, 70, [5, 2], 80, 0.35, 0.1);
    private static readonly Shape River = new(0.2, 80, 0.6, 2, 150, 200, [2.5, 1, 0.5], 80, 0, 0);

    private static readonly int[] Dx = [1, 0, -1, 0];
    private static readonly int LandLeft = WorldMap.Land.X;
    private static readonly int LandTop = WorldMap.Land.Y;
    private static readonly int LandRight = WorldMap.Land.X + WorldMap.Land.Width;
    private static readonly int LandBottom = WorldMap.Land.Y + WorldMap.Land.Height;
    private static readonly int ValeLeft = WorldMap.Vale.X;
    private static readonly int ValeTop = WorldMap.Vale.Y;
    private static readonly int ValeRight = WorldMap.Vale.X + WorldMap.Vale.Width;
    private static readonly int ValeBottom = WorldMap.Vale.Y + WorldMap.Vale.Height;

    private static readonly int[] Dy = [0, -1, 0, 1];
    private static readonly int[] Dx8 = [1, 1, 0, -1, -1, -1, 0, 1];
    private static readonly int[] Dy8 = [0, -1, -1, -1, 0, 1, 1, 1];
    private static readonly int[] Turn = [0, 1, 7, 2, 6, 3, 5, 4];

    /// <summary>Enough for every village a busy server or a parallel test run touches at once.</summary>
    private const int CachedSeeds = 64;

    private static readonly Dictionary<uint, (Lazy<Courses> Land, LinkedListNode<uint> Use)> Cache = new();
    private static readonly LinkedList<uint> CacheOrder = new();

    /// <summary>The land this thread looked at last, so repeated lookups on one seed skip the shared cache.</summary>
    [ThreadStatic]
    private static uint LastSeed;

    [ThreadStatic]
    private static Courses? LastCourses;
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

        return CoursesFor(seed).At(Key(x, y));
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

    /// <summary>
    /// Each seed's land is built once and shared. The most recently used seeds stay in memory,
    /// and one seed being built does not hold up lookups on another.
    /// </summary>
    private static Courses CoursesFor(uint seed)
    {
        if (LastCourses is not null && LastSeed == seed)
        {
            return LastCourses;
        }

        Lazy<Courses> entry;
        lock (CacheLock)
        {
            if (Cache.TryGetValue(seed, out var cached))
            {
                CacheOrder.Remove(cached.Use);
                CacheOrder.AddLast(cached.Use);
                entry = cached.Land;
            }
            else
            {
                entry = new Lazy<Courses>(() => Build(seed).Pack());
                Cache[seed] = (entry, CacheOrder.AddLast(seed));
                while (CacheOrder.Count > CachedSeeds)
                {
                    Cache.Remove(CacheOrder.First!.Value);
                    CacheOrder.RemoveFirst();
                }
            }
        }

        LastSeed = seed;
        LastCourses = entry.Value;

        return LastCourses;
    }

    private static Grid Build(uint seed)
    {
        var map = new Grid();

        void Paint(int x, int y, Kind kind)
        {
            if (!OnLand(x, y) || InVale(x, y))
            {
                return;
            }

            var prev = map.Get(x, y);
            if (prev != Kind.Open)
            {
                if (prev is Saddle or Kind.Rockfall)
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

            map.Set(x, y, kind);
        }

        int CountryReach(int id, int salt) => 120 + (int)Math.Floor(Roll(seed, id, salt, 91) * 160);

        bool ClearOfCamp(int x, int y, int reach)
        {
            var dx = x - 15;
            var dy = y - 13;
            return dx * dx + dy * dy >= reach * reach && !InVale(x, y);
        }

        void Open(int x, int y)
        {
            if (OnLand(x, y) && !InVale(x, y))
            {
                map.Set(x, y, Saddle);
            }
        }

        void Disk(int x, int y, double radius, Kind kind, Shape shape, int salt, int reach)
        {
            var span = (int)Math.Floor(radius);
            var inner = (radius - 1) * (radius - 1);
            var awayFromCamp = Math.Sqrt((x - 15.0) * (x - 15.0) + (y - 13.0) * (y - 13.0)) >= reach + span + 2;
            for (var oy = -span; oy <= span; oy++)
            {
                for (var ox = -span; ox <= span; ox++)
                {
                    var distance = ox * ox + oy * oy;
                    if (distance > radius * radius)
                    {
                        continue;
                    }

                    var tx = x + ox;
                    var ty = y + oy;

                    // A tile this paint could not change needs no rolls.
                    var prev = map.Get(tx, ty);
                    if (prev is Kind.Mountain or Kind.Rockfall || (prev != Kind.Open && kind is Kind.Thicket or Kind.Water))
                    {
                        continue;
                    }

                    if (radius >= 1.5 && distance > inner && Roll(seed, tx, ty, salt + 81) < shape.Edge)
                    {
                        continue;
                    }

                    if (shape.Skip > 0 && Roll(seed, tx, ty, salt + 80) < shape.Skip)
                    {
                        continue;
                    }

                    if (!awayFromCamp && !ClearOfCamp(tx, ty, reach))
                    {
                        continue;
                    }

                    Paint(tx, ty, kind);
                }
            }
        }

        (int X, int Y)? Start(int id, int salt, int room)
        {
            var span = WorldMap.Side - 400;
            var x = WorldMap.Land.X + 200 + (int)Math.Floor(Roll(seed, id, salt, 1) * span);
            var y = WorldMap.Land.Y + 200 + (int)Math.Floor(Roll(seed, id, salt, 2) * span);
            if (!ClearOfCamp(x, y, CountryReach(id, salt) + room))
            {
                return null;
            }

            return (x, y);
        }

        (int X, int Y)? Cell(int index, int salt, int region)
        {
            var regions = WorldMap.Side / region;
            var column = index % regions;
            var row = index / regions;
            var x = WorldMap.Land.X + column * region + 30 + (int)Math.Floor(Roll(seed, index, salt, 1) * (region - 60));
            var y = WorldMap.Land.Y + row * region + 30 + (int)Math.Floor(Roll(seed, index, salt, 2) * (region - 60));
            if (!ClearOfCamp(x, y, CountryReach(index, salt)))
            {
                return null;
            }

            return (x, y);
        }

        // Thin most of the way, easing up to a broad stretch now and then, narrowing at both ends. A river widens downstream.
        double HalfWidth(int id, int salt, int step, int length, Kind kind, int depth, Shape shape)
        {
            var knot = step / shape.Period;
            var t = (double)step / shape.Period - knot;
            var from = Roll(seed, id, knot, salt + 62);
            var to = Roll(seed, id, knot + 1, salt + 62);
            var swell = from + (to - from) * (t * t * (3 - 2 * t));
            var widest = shape.Swell[depth];
            if (kind == Kind.Water)
            {
                return widest * (0.15 + 0.85 * step / length) * (0.6 + 0.4 * swell);
            }

            var ends = Math.Min(1, Math.Min(step / 24.0, (length - step) / 24.0));
            return widest * swell * swell * ends;
        }

        void Grow(int id, int salt, int x, int y, int length, Kind kind, int depth, int dir, int reach, Shape shape, int childMin, int childSpan, double forkChance)
        {
            var home = dir;
            var gateEvery = 100 + (int)Math.Floor(Roll(seed, id, salt, 4) * 40);
            var load = 0.0;
            var sinceGate = 0;
            var gates = 0;
            var px = x;
            var py = y;
            for (var step = 0; step < length; step++)
            {
                if (!OnLand(px, py) || !ClearOfCamp(px, py, reach))
                {
                    break;
                }

                var radius = HalfWidth(id, salt, step, length, kind, depth, shape);
                load += 2 * radius + 1;
                sinceGate++;
                if (step > 6 && (load >= GateLoad || sinceGate >= gateEvery))
                {
                    load = 0;
                    sinceGate = 0;
                    var opening = kind != Kind.Mountain || gates % 2 == 1;
                    gates++;
                    void Mark(int gx, int gy)
                    {
                        if (opening)
                        {
                            Open(gx, gy);
                        }
                        else
                        {
                            Paint(gx, gy, Kind.Rockfall);
                        }
                    }

                    Mark(px, py);
                    var spoke = (int)Math.Floor(radius * 1.5) + 2;
                    for (var side = 0; side < 8; side += 2)
                    {
                        for (var n = 1; n <= spoke; n++)
                        {
                            Mark(px + Dx8[side] * n, py + Dy8[side] * n);
                        }
                    }
                }

                Disk(px, py, radius, kind, shape, salt, reach);
                if (depth < shape.MaxDepth && step > 5 && step % shape.ForkEvery == 0 && Roll(seed, id, step, salt + 70) < forkChance)
                {
                    var off = Roll(seed, id, step, salt + 71) < 0.5 ? 1 : 7;
                    var childDir = (dir + off) % 8;
                    var childLen = childMin + (int)Math.Floor(Roll(seed, id, step, salt + 73) * childSpan);
                    var childId = unchecked(id + (step + 1) * 1024 + (depth + 1) * 262144);
                    Grow(childId, salt, px, py, childLen, kind, depth + 1, childDir, reach, shape, Math.Max(8, childMin >> 1), Math.Max(8, childSpan >> 1), forkChance * 0.45);
                }

                if (step > 0 && step % 240 == 0 && Roll(seed, id, step, salt + 52) < 0.4)
                {
                    home = (home + (Roll(seed, id, step, salt + 53) < 0.5 ? 1 : 7)) % 8;
                }

                var drift = (dir - home + 8) % 8;
                var sway = Roll(seed, id, step, salt + 50);
                if (drift >= 2 && drift <= 6 && sway < 0.6)
                {
                    dir = (dir + (drift <= 4 ? 7 : 1)) % 8;
                }
                else if (sway < shape.Wobble)
                {
                    var side = Roll(seed, id, step, salt + 51) < 0.5 ? 1 : 7;
                    dir = (dir + side) % 8;
                }

                // A course ends short of the edge of the land rather than running along it, so it never walls off the edge.
                if (!OnLand(px + Dx8[dir] * EdgeStop, py + Dy8[dir] * EdgeStop))
                {
                    break;
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
                        Paint(px - Dx8[nd], py, kind);
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

        void Course(int id, int salt, int x, int y, int length, Kind kind, int reach, Shape shape)
        {
            var dir = (int)Math.Floor(Roll(seed, id, salt, 3) * 8);
            Grow(id, salt, x, y, length, kind, 0, dir, reach, shape, shape.ChildMin, shape.ChildSpan, shape.ForkChance);
        }

        // Turns the massif's mountain tiles on one line into an open valley, or into rockfall that opens once it is cleared.
        void Pass(int x, int y, int dx, int dy, int length, bool valley)
        {
            for (var n = 0; n <= length; n++)
            {
                if (map.Get(x + dx * n, y + dy * n) == Kind.Mountain)
                {
                    map.Set(x + dx * n, y + dy * n, valley ? Saddle : Kind.Rockfall);
                }
            }
        }

        for (var index = 0; index < Massifs; index++)
        {
            var radius = 50 + (int)Math.Floor(Roll(seed, index, 130, 6) * 60);
            if (Start(index, 130, radius * 2) is not { } origin)
            {
                continue;
            }

            var reach = CountryReach(index, 130);
            var lumps = 14 + (int)Math.Floor(Roll(seed, index, 130, 7) * 10);
            for (var lump = 0; lump < lumps; lump++)
            {
                var ox = (int)Math.Floor((Roll(seed, index, lump, 131) * 2 - 1) * radius * 0.75);
                var oy = (int)Math.Floor((Roll(seed, index, lump, 132) * 2 - 1) * radius * 0.75);
                var size = radius * (0.25 + Roll(seed, index, lump, 133) * 0.25);
                Disk(origin.X + ox, origin.Y + oy, size, Kind.Mountain, Range, 130, reach);
            }

            var arms = 2 + (int)Math.Floor(Roll(seed, index, 130, 8) * 2);
            for (var arm = 0; arm < arms; arm++)
            {
                var id = index * 8 + arm;
                var length = 400 + (int)Math.Floor(Roll(seed, id, 140, 5) * 500);
                Course(id, 140, origin.X, origin.Y, length, Kind.Mountain, reach, Range);
            }

            var left = origin.X - radius * 2;
            var top = origin.Y - radius * 2;
            var firstX = left + (int)Math.Floor(Roll(seed, index, 130, 9) * MassifPassSpacing);
            var firstY = top + (int)Math.Floor(Roll(seed, index, 130, 10) * MassifPassSpacing);
            for (int x = firstX, line = 0; x <= origin.X + radius * 2; x += MassifPassSpacing, line++)
            {
                Pass(x, top, 0, 1, radius * 4, line % 2 == 1);
            }

            for (int y = firstY, line = 0; y <= origin.Y + radius * 2; y += MassifPassSpacing, line++)
            {
                Pass(left, y, 1, 0, radius * 4, line % 2 == 1);
            }
        }

        var rangeSquares = WorldMap.Side / RangeRegion * (WorldMap.Side / RangeRegion);
        for (var index = 0; index < rangeSquares; index++)
        {
            if (Roll(seed, index, 70, 6) < 0.15 || Cell(index, 70, RangeRegion) is not { } origin)
            {
                continue;
            }

            var length = 1200 + (int)Math.Floor(Roll(seed, index, 70, 5) * 1200);
            Course(index, 70, origin.X, origin.Y, length, Kind.Mountain, CountryReach(index, 70), Range);
        }

        for (var index = 0; index < RiverCourses; index++)
        {
            if (Start(index, 110, 0) is not { } origin)
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
            var length = toEdge ? 2500 : 900 + (int)Math.Floor(Roll(seed, index, 110, 8) * 800);
            Course(index, 110, origin.X, origin.Y, length, Kind.Water, reach, River);
        }

        var thicketSquares = WorldMap.Side / ThicketRegion * (WorldMap.Side / ThicketRegion);
        for (var index = 0; index < thicketSquares; index++)
        {
            if (Roll(seed, index, 90, 6) < 0.4 || Cell(index, 90, ThicketRegion) is not { } origin)
            {
                continue;
            }

            var length = 200 + (int)Math.Floor(Roll(seed, index, 90, 5) * 300);
            Course(index, 90, origin.X, origin.Y, length, Kind.Thicket, CountryReach(index, 90), Thicket);
        }

        return map;
    }

    private static bool OnLand(int x, int y) => x >= LandLeft && y >= LandTop && x < LandRight && y < LandBottom;

    private static bool InVale(int x, int y) => x >= ValeLeft && y >= ValeTop && x < ValeRight && y < ValeBottom;

    /// <summary>The non-open tiles of one seed, packed as sorted tile keys with a byte per kind.</summary>
    private sealed class Courses
    {
        private readonly int[] keys;
        private readonly byte[] kinds;

        /// <param name="keys">Tile keys in ascending order.</param>
        public Courses(int[] keys, byte[] kinds)
        {
            this.keys = keys;
            this.kinds = kinds;
        }

        public Kind At(int key)
        {
            var index = Array.BinarySearch(keys, key);

            return index >= 0 ? (Kind)kinds[index] : Kind.Open;
        }
    }

    /// <summary>Kinds painted while the land is built, kept in small square chunks so a paint is an array write.</summary>
    private sealed class Grid
    {
        private const int Shift = 5;
        private const int Size = 1 << Shift;

        private readonly Dictionary<long, byte[]> chunks = new();
        private long lastKey = long.MinValue;
        private byte[]? last;

        public Kind Get(int x, int y) =>
            Chunk(x, y, false) is { } chunk ? (Kind)chunk[Index(x, y)] : Kind.Open;

        public void Set(int x, int y, Kind kind) => Chunk(x, y, true)![Index(x, y)] = (byte)kind;

        /// <summary>The painted tiles in ascending key order, which is column by column and top to bottom.</summary>
        public Courses Pack()
        {
            var keys = new List<int>();
            var kinds = new List<byte>();
            var columns = chunks.Keys.GroupBy(key => (int)(key >> 32)).OrderBy(column => column.Key);
            foreach (var column in columns)
            {
                var stack = column.OrderBy(key => (int)key).Select(key => ((int)key, chunks[key])).ToList();
                for (var dx = 0; dx < Size; dx++)
                {
                    foreach (var (row, chunk) in stack)
                    {
                        for (var dy = 0; dy < Size; dy++)
                        {
                            var kind = chunk[(dx << Shift) | dy];
                            if (kind is (byte)Kind.Open or (byte)Saddle)
                            {
                                continue;
                            }

                            keys.Add(Key((column.Key << Shift) + dx, (row << Shift) + dy));
                            kinds.Add(kind);
                        }
                    }
                }
            }

            return new Courses(keys.ToArray(), kinds.ToArray());
        }

        private static int Index(int x, int y) => ((x & (Size - 1)) << Shift) | (y & (Size - 1));

        private byte[]? Chunk(int x, int y, bool create)
        {
            var key = ((long)(x >> Shift) << 32) | (uint)(y >> Shift);
            if (key == lastKey && last is not null)
            {
                return last;
            }

            if (!chunks.TryGetValue(key, out var chunk))
            {
                if (!create)
                {
                    return null;
                }

                chunk = new byte[Size * Size];
                chunks[key] = chunk;
            }

            lastKey = key;
            last = chunk;

            return chunk;
        }
    }

    /// <summary>How a course grows. Matches <c>Shape</c> in farWilds.ts.</summary>
    /// <param name="Swell">The widest half-width at each depth of branching.</param>
    /// <param name="Period">Steps between one width and the next one the course eases toward.</param>
    /// <param name="Edge">Share of tiles left out along the outer edge of a wide stretch.</param>
    /// <param name="Skip">Share of tiles left out anywhere.</param>
    private sealed record Shape(
        double Wobble,
        int ForkEvery,
        double ForkChance,
        int MaxDepth,
        int ChildMin,
        int ChildSpan,
        double[] Swell,
        int Period,
        double Edge,
        double Skip);
}
