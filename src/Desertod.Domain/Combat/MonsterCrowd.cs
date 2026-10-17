using Desertod.Domain.World;
using RVO;

namespace Desertod.Domain.Combat;

public sealed record CrowdActor(int Id, TilePoint Position, TilePoint Goal, double Speed, double Radius = .2);

/// <summary>ORCA только предлагает позиции. Геометрия мира остаётся последним арбитром.</summary>
public static class MonsterCrowd
{
    public static bool CanTraverse(TilePoint from,TilePoint to,Func<int,int,bool> pass)
    {
        var x=(int)Math.Floor(from.X);var y=(int)Math.Floor(from.Y);
        var count=Math.Max(1,(int)Math.Ceiling(from.DistanceTo(to)*16));
        for(var i=1;i<=count;i++)
        {
            var nx=(int)Math.Floor(from.X+(to.X-from.X)*i/count);
            var ny=(int)Math.Floor(from.Y+(to.Y-from.Y)*i/count);
            if(!pass(nx,ny)||nx!=x&&ny!=y&&(!pass(nx,y)||!pass(x,ny)))return false;
            x=nx;y=ny;
        }
        return true;
    }

    public static IReadOnlyDictionary<int, TilePoint> Move(IReadOnlyList<CrowdActor> actors, double seconds,
        Func<TilePoint, TilePoint, bool> allowed)
    {
        if (actors.Count == 0) return new Dictionary<int, TilePoint>();
        var simulation = new Simulator { NumWorkers = 1 };
        foreach (var actor in actors)
            simulation.AddAgent(new((float)actor.Position.X, (float)actor.Position.Y), 3, 10, .5f, .5f,
                (float)actor.Radius, (float)actor.Speed, new(0, 0));
        // Короткие подшаги не перескакивают углы/стены при быстром локальном обходе.
        var steps = Math.Max(1, (int)Math.Ceiling(seconds / .25));
        simulation.TimeStep = (float)(seconds / steps);
        for (var step = 0; step < steps; step++)
        {
            var before = new TilePoint[actors.Count];
            for (var i = 0; i < actors.Count; i++)
            {
                var p = simulation.GetAgentPosition(i); before[i] = new(p.X, p.Y);
                var next = RaidNavigation.Step(before[i], actors[i].Goal, actors[i].Speed * simulation.TimeStep);
                simulation.SetAgentPrefVelocity(i, new((float)((next.X - before[i].X) / simulation.TimeStep),
                    (float)((next.Y - before[i].Y) / simulation.TimeStep)));
            }
            simulation.DoStep();
            for (var i = 0; i < actors.Count; i++)
            {
                var p = simulation.GetAgentPosition(i); var next = new TilePoint(p.X, p.Y);
                if (!double.IsFinite(next.X) || !double.IsFinite(next.Y) || !allowed(before[i], next))
                { simulation.SetAgentPosition(i, new((float)before[i].X, (float)before[i].Y)); simulation.SetAgentVelocity(i, new(0, 0)); }
            }
        }
        return actors.Select((actor, i) => {
            var p = simulation.GetAgentPosition(i); return (actor.Id, Point: new TilePoint(p.X, p.Y));
        }).ToDictionary(p => p.Id, p => p.Point);
    }
}
