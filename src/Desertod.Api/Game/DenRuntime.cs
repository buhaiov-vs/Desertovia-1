using System.Collections.Concurrent;
using Desertod.Domain.Combat;
using Desertod.Domain.World;

namespace Desertod.Api.Game;

/// <param name="ReturnsAt">When a slain protector stands at the den again.</param>
public sealed record DenProtectorDto(int Index, double X, double Y, double Health, double MaximumHealth, DenGuardState State, DateTimeOffset? ReturnsAt);

public sealed record DenHeroDto(Guid CitizenId, double X, double Y, double Health, double MaximumHealth, int? Target);

/// <summary>A den whose protectors are awake, walking home or slain. A den missing from the list is at rest.</summary>
/// <param name="Events">What happened since the previous snapshot of this den.</param>
public sealed record DenSnapshotDto(string DenId, IReadOnlyList<DenProtectorDto> Protectors, DenHeroDto? Hero, IReadOnlyList<DenEvent> Events)
{
    /// <summary>The den is at rest again: its viewers draw the protectors at home.</summary>
    public bool Resting { get; init; }
}

public sealed record DenAttackRequest(int Protector);

/// <summary>A den fight held in memory. Every access to <see cref="Fight"/> takes <see cref="Gate"/>.</summary>
public sealed class LiveDen(Guid villageId, string slug, DenFight fight)
{
    public Guid VillageId { get; } = villageId;
    public string Slug { get; } = slug;
    public DenFight Fight { get; } = fight;
    public Random Random { get; } = new();
    public Lock Gate { get; } = new();

    /// <summary>Account playing the hero now, for the hospital check.</summary>
    public Guid? HeroAccountId { get; set; }

    /// <summary>A citizen is being stopped in the database to come into this fight; the den must stay until they do.</summary>
    public bool Opening { get; set; }

    /// <summary>Outcomes not yet written to the database; kept here until the write succeeds.</summary>
    public List<DenOutcome> Unsettled { get; } = [];

    public string Id => Fight.Point.Id;

    public DenSnapshotDto Snapshot(IReadOnlyList<DenEvent> events) => new(
        Id,
        Fight.Protectors.Select(p => new DenProtectorDto(p.Index, Math.Round(p.X, 2), Math.Round(p.Y, 2), Math.Round(p.Health, 1), p.Guard.Health, p.State, p.ReturnsAt)).ToList(),
        Fight.Hero is { } hero
            ? new DenHeroDto(hero.CitizenId, Math.Round(hero.X, 2), Math.Round(hero.Y, 2), Math.Round(hero.Health, 1), hero.Fighter.Health, hero.Target)
            : null,
        events)
    {
        Resting = Fight.IsResting,
    };
}

/// <summary>
/// The den fights in this process; the server runs as a single instance. Slain protectors are kept here only,
/// so a restart brings every protector back.
/// </summary>
public sealed class DenRuntime
{
    private readonly ConcurrentDictionary<(Guid VillageId, string DenId), LiveDen> dens = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<SpawnPoint>> points = new();

    public IEnumerable<LiveDen> Dens => dens.Values;

    public IReadOnlyList<SpawnPoint> Points(Guid villageId) =>
        points.GetOrAdd(villageId, id => SpawnRings.Points(Wilderness.Seed(id.ToString())));

    /// <summary>Dens and roaming foes whose tile is within <paramref name="radius"/> of <paramref name="position"/>.</summary>
    public IEnumerable<SpawnPoint> Near(Guid villageId, TilePoint position, double radius)
    {
        var dens = Points(villageId);
        return dens.Where(p => p.Center.DistanceTo(position) <= radius)
            .Concat(Haunts.Near(Wilderness.Seed(villageId.ToString()), position, radius, dens));
    }

    public LiveDen Open(Guid villageId, string slug, SpawnPoint point)
    {
        var seed = Wilderness.Seed(villageId.ToString());
        return dens.GetOrAdd((villageId, point.Id), _ =>
            new LiveDen(villageId, slug, new DenFight(point, WorldMap.Places[WorldMap.Camp], (x, y) => !FarGround.Water(x, y, seed))));
    }

    public LiveDen? Find(Guid villageId, string denId) => dens.GetValueOrDefault((villageId, denId));

    /// <summary>The fight this citizen is in, if any.</summary>
    public LiveDen? Fighting(Guid citizenId) => dens.Values.FirstOrDefault(d => d.Fight.Fights(citizenId));

    public IReadOnlyList<DenSnapshotDto> Snapshots(Guid villageId)
    {
        var snapshots = new List<DenSnapshotDto>();
        foreach (var live in dens.Values.Where(d => d.VillageId == villageId))
        {
            lock (live.Gate)
            {
                if (!live.Fight.IsResting)
                {
                    snapshots.Add(live.Snapshot([]));
                }
            }
        }

        return snapshots;
    }

    public void Remove(LiveDen live) => dens.TryRemove(new KeyValuePair<(Guid, string), LiveDen>((live.VillageId, live.Id), live));
}
