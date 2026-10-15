using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

public sealed class RaidWaypoint
{
    public int X { get; set; }
    public int Y { get; set; }
}
public sealed record RaidApproach(string Sector, IReadOnlyList<RaidWaypoint> Path);

/// <summary>Bounded server paths on the kingdom side of the river; no teleporting through occupied plots or uncleared obstacles.</summary>
public static class RaidTactics
{
    public static readonly string[] Sectors = ["north", "west", "south"];
    public static IReadOnlyList<RaidApproach> Approaches(Village village)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        var target = ((int)Math.Floor(camp.X), (int)Math.Floor(camp.Y));
        var starts = new[] { ("north", (15, -3)), ("west", (-3, 13)), ("south", (15, 29)) };
        var occupied = village.Structures.Select(s => WorldMap.Footprint(s.PlotId)).Concat(village.Storages.Select(s => WorldMap.Footprint(s.PlotId)))
            .Concat(village.Sites.Select(s => WorldMap.Footprint(s.PlotId))).Where(a => a is not null).Select(a => a!.Value).ToList();
        var obstacles = (village.Wilds?.Obstacles ?? []).Where(Wilderness.Stands).Select(o => (o.X, o.Y)).ToHashSet();
        bool Pass((int X,int Y) p) => p.X >= -4 && p.X <= 28 && p.Y >= -4 && p.Y <= 30
            && p.X < Wilderness.RiverWestBank(p.Y, Wilderness.Seed(village.Id.ToString()))
            && !obstacles.Contains(p) && (p == target || !occupied.Any(a => a.Overlaps(new(p.X,p.Y,1,1))));
        var result = new List<RaidApproach>();
        foreach (var (sector, start) in starts)
        {
            if (!Pass(start)) continue;
            var queue = new Queue<(int,int)>(); queue.Enqueue(start);
            var previous = new Dictionary<(int,int),(int,int)> { [start] = start };
            while (queue.TryDequeue(out var point) && previous.Count <= 1200)
            {
                if (point == target) break;
                foreach (var next in new[] {(point.Item1+1,point.Item2),(point.Item1-1,point.Item2),(point.Item1,point.Item2+1),(point.Item1,point.Item2-1)})
                    if (Pass(next) && previous.TryAdd(next, point)) queue.Enqueue(next);
            }
            if (!previous.ContainsKey(target)) continue;
            var route = new List<RaidWaypoint>(); var step = target;
            while (true) { route.Add(new(){X=step.Item1,Y=step.Item2}); if(step==start)break; step=previous[step]; }
            route.Reverse(); result.Add(new(sector, route));
        }
        return result;
    }

    public static bool CanDefend(Citizen citizen, Raid raid) => raid.EntrySector is null ||
        !(citizen.DefenseReadyAt > raid.StrikesAt) && (string.IsNullOrEmpty(citizen.DefenseSector) || citizen.DefenseSector == raid.EntrySector);
}
