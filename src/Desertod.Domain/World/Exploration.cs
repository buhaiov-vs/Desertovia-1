namespace Desertod.Domain.World;

/// <summary>A walk across the map. The land along it is seen only as the walker gets there.</summary>
public sealed class Trip
{
    public double FromX { get; set; }
    public double FromY { get; set; }
    public double ToX { get; set; }
    public double ToY { get; set; }
    public double SpeedMultiplier { get; set; } = 1;
    public DateTimeOffset SetOutAt { get; set; }

    public TilePoint From => new(FromX, FromY);
    public TilePoint To => new(ToX, ToY);

    public DateTimeOffset ArrivesAt => SetOutAt + TimeSpan.FromSeconds(From.DistanceTo(To) * Exploration.SecondsPerTile / Math.Max(.1,SpeedMultiplier));

    public static Trip Start(TilePoint from, TilePoint to, DateTimeOffset now) =>
        new() { FromX = from.X, FromY = from.Y, ToX = to.X, ToY = to.Y, SetOutAt = now };

    /// <summary>Where the walker is: on the way, or at the end once arrived.</summary>
    public TilePoint PositionAt(DateTimeOffset now)
    {
        var total = (ArrivesAt - SetOutAt).TotalSeconds;
        var share = total <= 0 ? 1 : Math.Clamp((now - SetOutAt).TotalSeconds / total, 0, 1);
        return new TilePoint(FromX + (ToX - FromX) * share, FromY + (ToY - FromY) * share);
    }
}

/// <summary>One 32×32 block of seen land. A missing block is fog. <see cref="Bits"/> is null when every tile in the block has been seen.</summary>
public sealed record FogChunk(int X, int Y, bool Complete, byte[]? Bits);

/// <summary>
/// Fog of war. The tribe shares one set of seen chunks: whatever one citizen or the Tsar walks past, all players see.
/// A chunk that nobody has seen is not stored. Saved villages that still have the old dense bitmap are read once and
/// written back as chunks.
/// </summary>
public static class Exploration
{
    /// <summary>How far, in tiles, a walker sees around them.</summary>
    public const double SightRadius = 6;

    /// <summary>The camp land a new tribe knows: an oval around the campfire, reaching the river bank.</summary>
    public const double CampRadiusX = 19;

    public const double CampRadiusY = 17;

    /// <summary>Seen around buildings the tribe had before the fog of war, so none of them stands in the dark.</summary>
    public const double BuildingSightRadius = 4;

    /// <summary>The pace figures walk at on the map; the land ahead is seen no sooner than the map shows them there.</summary>
    public const double SecondsPerTile = 0.6;

    /// <summary>How far, in tiles, a walker sees from the top of the lookout peak.</summary>
    public const double LookoutSightRadius = 22;

    /// <summary>A walk that ends this close to the top of the lookout counts as climbing it.</summary>
    public const double LookoutReach = 1.5;

    /// <summary>Tiles on a side of one stored block.</summary>
    public const int ChunkSize = 32;

    /// <summary>Bytes in a block that is only partly seen. A fully seen block stores no bits.</summary>
    public const int ChunkBytes = ChunkSize * ChunkSize / 8;

    /// <summary>Length of the old dense bitmap, one bit per tile of <see cref="WorldMap.Land"/>.</summary>
    public static int LegacySize => (WorldMap.Land.Width * WorldMap.Land.Height + 7) / 8;

    /// <summary>Same as <see cref="LegacySize"/>. Kept so saved rows can still be recognized.</summary>
    public static int Size => LegacySize;

    private static ReadOnlySpan<byte> Magic => "FCH1"u8;

    /// <summary>What a new tribe has seen: the camp land.</summary>
    public static byte[] Camp()
    {
        var map = new ExploredChunks();
        map.RevealOval(WorldMap.Places[WorldMap.Camp], CampRadiusX, CampRadiusY);
        return map.Encode();
    }

    /// <summary>Every tile of the land, as complete chunks. Development villages and tests use this.</summary>
    public static byte[] EntireLand()
    {
        var map = new ExploredChunks();
        var land = WorldMap.Land;
        var x0 = ChunkOf(land.X);
        var x1 = ChunkOf(land.X + land.Width - 1);
        var y0 = ChunkOf(land.Y);
        var y1 = ChunkOf(land.Y + land.Height - 1);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                map.MarkComplete(x, y);
            }
        }

        return map.Encode();
    }

    public static bool IsChunkFormat(byte[] map) =>
        map.Length >= Magic.Length && map.AsSpan(0, Magic.Length).SequenceEqual(Magic);

    public static bool IsExplored(byte[] map, int x, int y)
    {
        var land = WorldMap.Land;
        if (x < land.X || y < land.Y || x >= land.X + land.Width || y >= land.Y + land.Height)
        {
            return false;
        }

        if (IsChunkFormat(map))
        {
            return Decode(map).IsExplored(x, y);
        }

        if (map.Length != LegacySize)
        {
            return false;
        }

        var index = (y - land.Y) * land.Width + (x - land.X);
        return (map[index >> 3] & (1 << (index & 7))) != 0;
    }

    /// <summary>Whether one saved chunk contains the tile. Other chunks are ignored.</summary>
    public static bool ChunkSees(int chunkX, int chunkY, bool complete, byte[]? bits, int x, int y)
    {
        if (ChunkOf(x) != chunkX || ChunkOf(y) != chunkY)
        {
            return false;
        }

        if (complete)
        {
            return true;
        }

        if (bits is null || bits.Length != ChunkBytes)
        {
            return false;
        }

        var index = (y - chunkY * ChunkSize) * ChunkSize + (x - chunkX * ChunkSize);
        return (uint)index < (uint)(ChunkSize * ChunkSize) && (bits[index >> 3] & (1 << (index & 7))) != 0;
    }

    /// <summary>True when every tile of the area has been seen.</summary>
    public static bool IsExplored(byte[] map, TileArea area)
    {
        for (var y = area.Y; y < area.Y + area.Height; y++)
        {
            for (var x = area.X; x < area.X + area.Width; x++)
            {
                if (!IsExplored(map, x, y))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Reads a saved dense bitmap into chunks. The bitmap itself is left unchanged.</summary>
    public static byte[] ImportLegacy(byte[] legacy)
    {
        var map = new ExploredChunks();
        if (legacy.Length != LegacySize)
        {
            return map.Encode();
        }

        var land = WorldMap.Land;
        for (var y = land.Y; y < land.Y + land.Height; y++)
        {
            for (var x = land.X; x < land.X + land.Width; x++)
            {
                var index = (y - land.Y) * land.Width + (x - land.X);
                if ((legacy[index >> 3] & (1 << (index & 7))) != 0)
                {
                    map.Set(x, y);
                }
            }
        }

        return map.Encode();
    }

    /// <summary>Marks the circle around <paramref name="center"/> and returns the chunks.</summary>
    public static byte[] Reveal(byte[] map, TilePoint center, double radius)
    {
        var chunks = Decode(map);
        chunks.RevealOval(center, radius, radius);
        return chunks.Encode();
    }

    /// <summary>
    /// Marks the land seen along the part of the trip walked by <paramref name="now"/>, and the wide view from the
    /// <paramref name="lookout"/> once the trip ends on top of it. Returns the chunks; the passed buffer is not changed.
    /// </summary>
    public static byte[] Reveal(byte[] map, Trip trip, DateTimeOffset now, TilePoint? lookout = null)
    {
        var chunks = Decode(map);
        if (now < trip.SetOutAt)
        {
            return chunks.Encode();
        }

        if (lookout is { } peak && now >= trip.ArrivesAt && trip.To.DistanceTo(peak) <= LookoutReach)
        {
            chunks.RevealOval(peak, LookoutSightRadius, LookoutSightRadius);
        }

        chunks.RevealSegment(trip.From, trip.PositionAt(now), SightRadius);
        return chunks.Encode();
    }

    /// <summary>The saved chunks, for the village state. Only blocks someone has seen are included.</summary>
    public static IReadOnlyList<FogChunk> Chunks(byte[] map) => Decode(map).List();

    /// <summary>
    /// The same seen land as one bit per tile of <see cref="WorldMap.Land"/>, the layout the current client decodes.
    /// The map is still 120 by 90, so this stays a small bitmap beside the chunks.
    /// </summary>
    public static byte[] LegacyBitmap(byte[] map)
    {
        var seen = Decode(map);
        var legacy = new byte[LegacySize];
        var land = WorldMap.Land;
        for (var y = land.Y; y < land.Y + land.Height; y++)
        {
            for (var x = land.X; x < land.X + land.Width; x++)
            {
                if (!seen.IsExplored(x, y))
                {
                    continue;
                }

                var index = (y - land.Y) * land.Width + (x - land.X);
                legacy[index >> 3] |= (byte)(1 << (index & 7));
            }
        }

        return legacy;
    }

    /// <summary>
    /// The village's seen land plus the parts of the trips walked by <paramref name="now"/>. Trips still count after
    /// they end, until the next trip of the same walker writes them into the village's map.
    /// </summary>
    public static byte[] Seen(byte[] explored, IEnumerable<Trip> trips, DateTimeOffset now, TilePoint? lookout = null)
    {
        var map = IsChunkFormat(explored) || explored.Length == LegacySize ? explored : Camp();
        foreach (var trip in trips)
        {
            map = Reveal(map, trip, now, lookout);
        }

        return map;
    }

    /// <summary>A walk that has written the previous trip into <see cref="Departure.Explored"/>.</summary>
    public readonly record struct Departure(Trip Trip, byte[] Explored);

    /// <summary>
    /// A walker sets out to <paramref name="to"/>: the part of their last trip walked so far is written into the
    /// village's map, and the new trip starts where they are: on the way, or at <paramref name="resting"/>.
    /// </summary>
    public static Departure SetOut(byte[] explored, Trip? current, TilePoint resting, TilePoint to, DateTimeOffset now, TilePoint? lookout = null)
    {
        var from = resting;
        var map = explored;
        if (current is not null)
        {
            map = Reveal(map, current, now, lookout);
            if (now < current.ArrivesAt)
            {
                from = current.PositionAt(now);
            }
        }

        return new Departure(Trip.Start(from, to, now), map);
    }

    private static int ChunkOf(int tile) => (int)Math.Floor(tile / (double)ChunkSize);

    private static ExploredChunks Decode(byte[] map)
    {
        if (IsChunkFormat(map))
        {
            return ExploredChunks.Decode(map);
        }

        if (map.Length == LegacySize)
        {
            return ExploredChunks.Decode(ImportLegacy(map));
        }

        return new ExploredChunks();
    }

    /// <summary>Seen chunks. A null entry is a block whose every tile has been seen.</summary>
    private sealed class ExploredChunks
    {
        private readonly Dictionary<(int X, int Y), byte[]?> chunks = new();

        public static ExploredChunks Decode(byte[] map)
        {
            var decoded = new ExploredChunks();
            var count = BitConverter.ToInt32(map, Magic.Length);
            var cursor = Magic.Length + sizeof(int);
            for (var i = 0; i < count; i++)
            {
                var x = BitConverter.ToInt16(map, cursor);
                var y = BitConverter.ToInt16(map, cursor + sizeof(short));
                var complete = map[cursor + sizeof(short) * 2] == 1;
                cursor += sizeof(short) * 2 + 1;
                byte[]? bits = null;
                if (!complete)
                {
                    bits = map.AsSpan(cursor, ChunkBytes).ToArray();
                    cursor += ChunkBytes;
                }

                decoded.chunks[(x, y)] = bits;
            }

            return decoded;
        }

        public byte[] Encode()
        {
            var ordered = chunks.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X).ToArray();
            var size = Magic.Length + sizeof(int);
            foreach (var pair in ordered)
            {
                size += sizeof(short) * 2 + 1 + (pair.Value is null ? 0 : ChunkBytes);
            }

            var map = new byte[size];
            Magic.CopyTo(map);
            BitConverter.TryWriteBytes(map.AsSpan(Magic.Length), ordered.Length);
            var cursor = Magic.Length + sizeof(int);
            foreach (var ((x, y), bits) in ordered)
            {
                BitConverter.TryWriteBytes(map.AsSpan(cursor), (short)x);
                BitConverter.TryWriteBytes(map.AsSpan(cursor + sizeof(short)), (short)y);
                map[cursor + sizeof(short) * 2] = bits is null ? (byte)1 : (byte)0;
                cursor += sizeof(short) * 2 + 1;
                if (bits is not null)
                {
                    bits.CopyTo(map, cursor);
                    cursor += ChunkBytes;
                }
            }

            return map;
        }

        public IReadOnlyList<FogChunk> List() => chunks
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => new FogChunk(pair.Key.X, pair.Key.Y, pair.Value is null, pair.Value))
            .ToArray();

        public bool IsExplored(int x, int y)
        {
            if (!chunks.TryGetValue((ChunkOf(x), ChunkOf(y)), out var bits))
            {
                return false;
            }

            if (bits is null)
            {
                return true;
            }

            var index = LocalIndex(x, y);
            return (bits[index >> 3] & (1 << (index & 7))) != 0;
        }

        public void MarkComplete(int chunkX, int chunkY) => chunks[(chunkX, chunkY)] = null;

        public void Set(int x, int y)
        {
            var land = WorldMap.Land;
            if (x < land.X || y < land.Y || x >= land.X + land.Width || y >= land.Y + land.Height)
            {
                return;
            }

            var key = (ChunkOf(x), ChunkOf(y));
            if (!chunks.TryGetValue(key, out var bits))
            {
                bits = new byte[ChunkBytes];
                chunks[key] = bits;
            }

            if (bits is null)
            {
                return;
            }

            var index = LocalIndex(x, y);
            bits[index >> 3] |= (byte)(1 << (index & 7));
            if (bits.All(part => part == byte.MaxValue))
            {
                chunks[key] = null;
            }
        }

        public void RevealOval(TilePoint center, double radiusX, double radiusY)
        {
            for (var y = (int)Math.Floor(center.Y - radiusY); y <= (int)Math.Ceiling(center.Y + radiusY); y++)
            {
                for (var x = (int)Math.Floor(center.X - radiusX); x <= (int)Math.Ceiling(center.X + radiusX); x++)
                {
                    var dx = (x + 0.5 - center.X) / radiusX;
                    var dy = (y + 0.5 - center.Y) / radiusY;
                    if (dx * dx + dy * dy <= 1)
                    {
                        Set(x, y);
                    }
                }
            }
        }

        /// <summary>
        /// Marks tiles whose center is within <paramref name="radius"/> of the segment. Samples along the segment
        /// choose the chunks to touch, so a long walk does not scan the rectangle around the whole trip.
        /// </summary>
        public void RevealSegment(TilePoint from, TilePoint to, double radius)
        {
            const double stride = 4;
            var length = from.DistanceTo(to);
            var samples = Math.Max(1, (int)Math.Ceiling(length / stride));
            var reach = radius + stride;
            for (var sample = 0; sample <= samples; sample++)
            {
                var share = (double)sample / samples;
                var center = new TilePoint(from.X + (to.X - from.X) * share, from.Y + (to.Y - from.Y) * share);
                var left = (int)Math.Floor(center.X - reach);
                var right = (int)Math.Ceiling(center.X + reach);
                var top = (int)Math.Floor(center.Y - reach);
                var bottom = (int)Math.Ceiling(center.Y + reach);
                for (var y = top; y <= bottom; y++)
                {
                    for (var x = left; x <= right; x++)
                    {
                        if (DistanceToSegment(new TilePoint(x + 0.5, y + 0.5), from, to) <= radius)
                        {
                            Set(x, y);
                        }
                    }
                }
            }
        }

        private static int LocalIndex(int x, int y)
        {
            var localX = x - ChunkOf(x) * ChunkSize;
            var localY = y - ChunkOf(y) * ChunkSize;
            return localY * ChunkSize + localX;
        }

        private static double DistanceToSegment(TilePoint point, TilePoint from, TilePoint to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var lengthSquared = dx * dx + dy * dy;
            var share = lengthSquared == 0 ? 0 : Math.Clamp(((point.X - from.X) * dx + (point.Y - from.Y) * dy) / lengthSquared, 0, 1);
            return point.DistanceTo(new TilePoint(from.X + dx * share, from.Y + dy * share));
        }
    }
}
