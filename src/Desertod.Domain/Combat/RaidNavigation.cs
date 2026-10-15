using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

/// <summary>One server geometry for movement, attack visibility and breakable barriers. No diagonal corner cutting.</summary>
public sealed class RaidNavigation
{
    private readonly Village village;
    private readonly HashSet<(int X, int Y)> solid = [];
    private readonly Dictionary<(int X, int Y), Fortification> walls;
    private readonly uint seed;
    private readonly byte[] terrain;
    private readonly Dictionary<(int X, int Y), bool> land = [];

    public RaidNavigation(Village village)
    {
        this.village = village;
        seed = Wilderness.Seed(village.Id.ToString());
        terrain=AuthoredTerrain.For(village.Id,village.Wilds);
        walls = village.Fortifications.ToDictionary(f => (f.X, f.Y));
        foreach (var plot in village.Structures.Where(s=>s.Kind!=BuildingKind.RopeBridge).Select(s => s.PlotId).Concat(village.Storages.Select(s => s.PlotId)).Concat(village.Sites.Select(s => s.PlotId)))
            if (WorldMap.PlotPosition(plot) is {} p)
                for (var x = (int)p.X; x < p.X + WorldMap.PlotWidth; x++)
                for (var y = (int)p.Y; y < p.Y + WorldMap.PlotHeight - 1; y++) solid.Add((x, y));
        foreach (var o in village.Wilds?.Obstacles.Where(Wilderness.Stands) ?? []) solid.Add((o.X, o.Y));
    }

    public bool Dry(int x, int y)
    {
        if (land.TryGetValue((x,y), out var valid)) return valid;
        // Raids use the kingdom's river bank. An enemy has no implicit swimming or closed-bridge permission.
        valid = AuthoredTerrain.Inside(x,y) && x + .5 < Wilderness.RiverWestBank(y,seed);
        if(valid)
        {
            var kind=terrain[AuthoredTerrain.Index(x,y)];
            valid=kind is not (AuthoredTerrain.Water or AuthoredTerrain.Mountain)
                && (kind!=AuthoredTerrain.Ravine || village.Structures.Any(s=>s.Kind==BuildingKind.RopeBridge
                    && WorldMap.Footprint(s.PlotId) is {} bridge && bridge.Contains(new(x,y,1,1))));
        }
        land[(x,y)] = valid;
        return valid;
    }
    public bool Pass(int x, int y) => Dry(x,y) && !solid.Contains((x,y)) && !(walls.GetValueOrDefault((x,y))?.Blocks ?? false);
    public Fortification? Barrier(int x,int y) => walls.GetValueOrDefault((x,y));

    public TilePoint Entry(string sector)
    {
        var area = KingdomRules.Area(village);
        var wish = sector switch
        {
            "north" => new TilePoint(15.5, area.Y - 6.5),
            "south" => new TilePoint(15.5, area.Y + area.Height + 6.5),
            _ => new TilePoint(area.X - 6.5, 13.5)
        };
        return NearestFree(wish,p=>IsOutside(p,sector));
    }
    public bool IsOutside(TilePoint p,string sector)
    {
        var area=KingdomRules.Area(village);
        return sector switch { "north"=>p.Y<area.Y-2, "south"=>p.Y>area.Y+area.Height+2, _=>p.X<area.X-2 };
    }
    public TilePoint NearestFree(TilePoint wish,Func<TilePoint,bool>? allowed=null)
    {
        for (var radius=0; radius<=12; radius++)
        for (var x=(int)Math.Floor(wish.X)-radius; x<=(int)Math.Floor(wish.X)+radius; x++)
        for (var y=(int)Math.Floor(wish.Y)-radius; y<=(int)Math.Floor(wish.Y)+radius; y++)
            if (Pass(x,y) && (allowed is null || allowed(new(x+.5,y+.5)))) return new(x+.5,y+.5);
        throw new GameRuleException("raid.noApproach","No free position on this approach.");
    }
    public static TilePoint GuardPoint(string? sector) => sector switch
    {
        "north" => new(15.5,1.5), "south" => new(15.5,24.5), "west" => new(1.5,13.5),
        _ => WorldMap.Places[WorldMap.Camp]
    };

    public bool ClearShot(TilePoint from,TilePoint to)
    {
        var steps=Math.Max(1,(int)Math.Ceiling(from.DistanceTo(to)*4));
        for(var i=1;i<steps;i++)
        {
            var x=(int)Math.Floor(from.X+(to.X-from.X)*i/steps);
            var y=(int)Math.Floor(from.Y+(to.Y-from.Y)*i/steps);
            if(!Pass(x,y))return false;
        }
        return true;
    }

    public TilePoint? Next(TilePoint from,TilePoint target,bool canBreach,bool preferBreach=false)
    {
        var start=((int)Math.Floor(from.X),(int)Math.Floor(from.Y));
        var end=((int)Math.Floor(target.X),(int)Math.Floor(target.Y));
        if(start==end)return target;
        var centre=new TilePoint(start.Item1+.5,start.Item2+.5);
        if(from.DistanceTo(centre)>.01)return centre;
        var queue=new PriorityQueue<(int,int),double>();queue.Enqueue(start,0);
        var cost=new Dictionary<(int,int),double>{{start,0}};
        var previous=new Dictionary<(int,int),(int,int)>();
        var found=false;
        while(queue.TryDequeue(out var p,out _) && cost.Count<=5000)
        {
            if(p==end){found=true;break;}
            foreach(var n in new[]{(p.Item1+1,p.Item2),(p.Item1-1,p.Item2),(p.Item1,p.Item2+1),(p.Item1,p.Item2-1)})
            {
                if(!Dry(n.Item1,n.Item2)||solid.Contains(n))continue;
                var wall=walls.GetValueOrDefault(n);
                if(wall?.Blocks==true&&!canBreach)continue;
                var nextCost=cost[p]+1+(wall?.Blocks==true?(100-wall.Damage)*(preferBreach ? .06 : .35):0);
                if(cost.TryGetValue(n,out var old)&&old<=nextCost)continue;
                cost[n]=nextCost;previous[n]=p;
                queue.Enqueue(n,nextCost+Math.Abs(n.Item1-end.Item1)+Math.Abs(n.Item2-end.Item2));
            }
        }
        if(!found)return null;
        var step=end;
        while(previous[step]!=start)step=previous[step];
        return new TilePoint(step.Item1+.5,step.Item2+.5);
    }
    public bool Move(RaidUnit unit,TilePoint goal,double seconds,bool canBreach,bool preferBreach,out Fortification? barrier)
    {
        barrier=null;
        var next=Next(unit.Position,goal,canBreach,preferBreach);
        if(next is null)return false;
        var wall=Barrier((int)Math.Floor(next.Value.X),(int)Math.Floor(next.Value.Y));
        if(wall?.Blocks==true){barrier=wall;return true;}
        var p=Step(unit.Position,next.Value,unit.Speed*seconds);unit.X=p.X;unit.Y=p.Y;return true;
    }
    public void Move(RaidFighter fighter,TilePoint goal,double seconds)
    {
        var next=Next(fighter.Position,goal,false);
        if(next is null)return;
        var p=Step(fighter.Position,next.Value,.65*seconds);fighter.X=p.X;fighter.Y=p.Y;
    }
    public static TilePoint Step(TilePoint from,TilePoint to,double distance)
    {
        var gap=from.DistanceTo(to);if(gap<=distance||gap<.001)return to;
        return new(from.X+(to.X-from.X)*distance/gap,from.Y+(to.Y-from.Y)*distance/gap);
    }
}
