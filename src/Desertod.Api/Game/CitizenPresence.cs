using Desertod.Domain.Rules;

namespace Desertod.Api.Game;

/// <summary>
/// Which citizens are around: their village page is connected to the hub, or they connected or acted within
/// <see cref="GameRules.PresenceWindow"/>. Kept in memory like the village locks, so after a restart everyone is away
/// until they come back.
/// </summary>
public sealed class CitizenPresence(TimeProvider time)
{
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, Entry> citizens = [];
    private readonly Dictionary<string, Guid> connections = [];

    /// <param name="Slug">Village the citizen lives in, for the viewers to hear they went away.</param>
    private sealed class Entry(string slug)
    {
        public string Slug { get; } = slug;
        public HashSet<string> Connections { get; } = [];
        public DateTimeOffset LastSeenAt { get; set; }
        public bool IsAway { get; set; }
    }

    /// <summary>The citizen's village page connected. Returns true when they were away until now.</summary>
    public bool Connect(Guid citizenId, string slug, string connectionId)
    {
        lock (gate)
        {
            var (entry, cameBack) = See(citizenId, slug);
            entry.Connections.Add(connectionId);
            connections[connectionId] = citizenId;
            return cameBack;
        }
    }

    /// <summary>The citizen acted. Returns true when they were away until now.</summary>
    public bool Touch(Guid citizenId, string slug)
    {
        lock (gate)
        {
            return See(citizenId, slug).CameBack;
        }
    }

    /// <summary>The page closed or left the village; the citizen stays present for <see cref="GameRules.PresenceWindow"/>.</summary>
    public void Disconnect(string connectionId)
    {
        lock (gate)
        {
            if (!connections.Remove(connectionId, out var citizenId) || !citizens.TryGetValue(citizenId, out var entry))
            {
                return;
            }

            entry.Connections.Remove(connectionId);
            entry.LastSeenAt = time.GetUtcNow();
        }
    }

    /// <summary>The citizen left the village.</summary>
    public void Forget(Guid citizenId)
    {
        lock (gate)
        {
            if (citizens.Remove(citizenId, out var entry))
            {
                foreach (var connectionId in entry.Connections)
                {
                    connections.Remove(connectionId);
                }
            }
        }
    }

    public bool IsPresent(Guid citizenId)
    {
        lock (gate)
        {
            return citizens.TryGetValue(citizenId, out var entry) && IsPresent(entry, time.GetUtcNow());
        }
    }

    /// <summary>Citizens who went away since the last call, each reported once until they come back.</summary>
    public IReadOnlyList<(string Slug, Guid CitizenId)> TakeDeparted()
    {
        lock (gate)
        {
            var now = time.GetUtcNow();
            var departed = new List<(string, Guid)>();
            foreach (var (citizenId, entry) in citizens)
            {
                if (!entry.IsAway && !IsPresent(entry, now))
                {
                    entry.IsAway = true;
                    departed.Add((entry.Slug, citizenId));
                }
            }

            return departed;
        }
    }

    private (Entry Entry, bool CameBack) See(Guid citizenId, string slug)
    {
        var now = time.GetUtcNow();
        if (!citizens.TryGetValue(citizenId, out var entry))
        {
            entry = new Entry(slug) { LastSeenAt = now };
            citizens[citizenId] = entry;
            return (entry, true);
        }

        var cameBack = entry.IsAway || !IsPresent(entry, now);
        entry.IsAway = false;
        entry.LastSeenAt = now;
        return (entry, cameBack);
    }

    private static bool IsPresent(Entry entry, DateTimeOffset now) =>
        entry.Connections.Count > 0 || now - entry.LastSeenAt < GameRules.PresenceWindow;
}
