using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

public sealed record FortificationQuote(int Level,int? NextLevel,int Sections,double Strength,int Damaged,
    int Work,IReadOnlyList<ResourceAmount> Materials,string? BlockedBy,bool ReadyAfterBattle);

public static class FortificationUpgrades
{
    public const string Plot="fortifications";
    public const string Operation="fortification-upgrade";
    public static int Level(Village village)=>village.Fortifications.Count==0?0:
        village.Fortifications.Min(f=>Math.Clamp(f.Level,1,3));

    public static FortificationQuote Quote(Village village,Guid? ignoreOrder=null)
    {
        var level=Level(village);var target=Math.Min(3,level+1);
        var sections=village.Fortifications.Count(f=>Math.Clamp(f.Level,1,3)<target);
        var scale=sections/80d;
        var materials=target==2?new[]{new ResourceAmount(ResourceKind.Wood,(int)Math.Ceiling(240*scale)),new ResourceAmount(ResourceKind.Planks,(int)Math.Ceiling(80*scale))}
            :new[]{new ResourceAmount(ResourceKind.Stone,(int)Math.Ceiling(400*scale)),new ResourceAmount(ResourceKind.Wood,(int)Math.Ceiling(80*scale)),new ResourceAmount(ResourceKind.Planks,(int)Math.Ceiling(80*scale))};
        var site=village.Sites.FirstOrDefault(s=>s.PlotId==Plot);
        var queued=village.CivicOrders.Any(o=>o.Id!=ignoreOrder&&o.PlotId==Plot&&o.Status is "pending" or "working");
        var pending=village.CivicOrders.Count(o=>o.Id!=ignoreOrder&&o.Status=="pending"&&o.Operation is "build" or "upgrade" or "demolish"
            &&!village.Structures.Any(s=>s.PlotId==o.PlotId&&s.OwnerAccountId is not null)&&!village.Storages.Any(s=>s.PlotId==o.PlotId&&s.OwnerAccountId is not null));
        var blocked=level==0?"defense.noWall":level>=3?"building.maxLevel":site is not null?"defense.upgradeWorking":queued?"building.orderExists"
            :village.Raids.Any(r=>r.IsPending)?"defense.raidPending":village.DefeatedAt is not null?"village.defeated"
            :!TechnologyRules.Knows(village,TechnologyCatalog.Carpentry)||target==3&&!TechnologyRules.Knows(village,TechnologyCatalog.Mining)?"defense.upgradeTechnology"
            :village.Sites.Count(s=>s.OwnerAccountId is null)+pending>=BuildingCatalog.MaximumOpenSites?"building.requestQueueFull":null;
        return new(level,level>=3?null:target,sections,level==3?3.5:level==2?2:1,
            village.Fortifications.Count(f=>f.Damage>0),Math.Max(target==2?25:60,(int)Math.Ceiling(sections*(target==2?.5:.75))),materials,blocked,
            site?.Operation==Operation&&site.IsFinished&&village.Raids.Any(r=>r.IsPending));
    }

    public static CivicOrder Request(Village village,Guid actor,DateTimeOffset now)
    {
        var quote=Quote(village);Require(quote);
        var order=new CivicOrder{PlotId=Plot,Operation="upgrade",BuildingKind=BuildingKind.FortificationUpgrade,RequestedBy=actor,CreatedAt=now};
        village.CivicOrders.Add(order);return order;
    }

    public static void Begin(Village village,CivicOrder order,DateTimeOffset now)
    {
        if(order.Status!="pending"||order.PlotId!=Plot||order.BuildingKind!=BuildingKind.FortificationUpgrade)
            throw new GameRuleException("building.orderProcessed","This fortification application is no longer pending.");
        var quote=Quote(village,order.Id);Require(quote);
        village.Sites.Add(new(){Kind=BuildingKind.FortificationUpgrade,PlotId=Plot,Operation=Operation,OrderId=order.Id,
            TargetLevel=quote.NextLevel!.Value,Scale=quote.Sections/80d,OperationWork=quote.Work,PlacedAt=now,PlacedByAccountId=order.RequestedBy});
        order.Status="working";order.ApprovedAt=now;
    }

    private static void Require(FortificationQuote quote)
    {
        if(quote.BlockedBy is {} code)throw new GameRuleException(code,"Fortification upgrade is unavailable. Check construction, technologies and raids.");
    }

    public static void ApplyReady(Village village)
    {
        if(village.Fortifications.Count==0||village.Raids.Any(r=>r.IsPending))return;
        foreach(var site in village.Sites.Where(s=>s.Operation==Operation&&s.IsFinished).ToArray())
        {
            foreach(var wall in village.Fortifications)wall.Level=Math.Max(Math.Clamp(wall.Level,1,3),Math.Clamp(site.TargetLevel,1,3));
            var order=village.CivicOrders.FirstOrDefault(o=>o.Id==site.OrderId);
            if(order is not null)order.Status="completed";
            village.Sites.Remove(site);
        }
    }
}
