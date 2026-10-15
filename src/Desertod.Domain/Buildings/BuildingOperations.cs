using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
namespace Desertod.Domain.Buildings;

public sealed class CivicOrder
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string PlotId { get; set; }
    public required string Operation { get; set; }
    public Guid RequestedBy { get; set; }
    public Guid? PartnerAccountId { get; set; }
    public bool PartnerAccepted { get; set; }
    public string Status { get; set; } = "pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public static class BuildingOperations
{
    public static BuildingDefinition Definition(ConstructionSite site)
    {
        var original=BuildingCatalog.Get(site.Kind);
        StageJob Haul(string id,int amount)=>new(id,amount);
        if(site.Operation=="demolish") return original with { Stages=[new("dismantle","Dismantling",1,[new("dismantle",site.OperationWork)])] };
        if(site.Operation=="storage-upgrade") return original with { Stages=[new("upgrade","Storage upgrade",3,
            [Haul("bring-logs",60),Haul("bring-stones",40),Haul("bring-planks",20),new("raise-posts",24)])] };
        if(site.Operation=="housing-upgrade")
        {
            var last=site.TargetLevel==3;
            return original with { Stages=[new("expand","Housing expansion",3,
                [Haul("bring-logs",last?160:100),Haul("bring-stones",last?120:80),Haul("bring-planks",last?60:30),
                 Haul("bring-reeds",last?60:40),new("raise-posts",last?80:60)])] };
        }
        return original;
    }

    public static CivicOrder Request(Village village,Guid actor,string plot,string operation,DateTimeOffset now)
    {
        if(operation is not ("demolish" or "upgrade")) throw new GameRuleException("building.operation","Unknown building action.");
        if(village.CivicOrders.Any(o=>o.PlotId==plot&&o.Status is "pending" or "working"))
            throw new GameRuleException("building.orderExists","An application already exists for this building.");
        if(!village.Structures.Any(s=>s.PlotId==plot)&&!village.Storages.Any(s=>s.PlotId==plot))
            throw new GameRuleException("building.notBuilt","Select a completed building.");
        var order=new CivicOrder{PlotId=plot,Operation=operation,RequestedBy=actor,CreatedAt=now};
        village.CivicOrders.Add(order);return order;
    }

    public static void Approve(Village village,CivicOrder order,IReadOnlyList<Citizen> citizens,DateTimeOffset now)
    {
        if(order.Status!="pending") throw new GameRuleException("building.orderProcessed","This application was already processed.");
        if(village.Sites.Count>=BuildingCatalog.MaximumOpenSites||village.Sites.Any(s=>s.PlotId==order.PlotId))
            throw new GameRuleException("building.busy","Finish the existing work first.");
        var structure=village.Structures.SingleOrDefault(s=>s.PlotId==order.PlotId);
        var storage=village.Storages.SingleOrDefault(s=>s.PlotId==order.PlotId);
        var kind=structure?.Kind??(storage is not null?BuildingCatalog.ForStorage(storage.Kind):null)
            ??throw new GameRuleException("building.protected","This is a protected civic building.");
        if(WorldMap.PlotPosition(order.PlotId) is null||kind is BuildingKind.RopeBridge or BuildingKind.Hospital)
            throw new GameRuleException("building.protected","This is a protected building or crossing.");
        if(citizens.Any(c=>!c.CycleSettled&&(c.CurrentSiteId==order.PlotId ||
            GameCatalog.FindTask(c.CurrentTaskId??"")?.RequiredBuilding==kind)))
            throw new GameRuleException("building.inUse","Finish the work inside this building first.");
        var site=new ConstructionSite{Kind=kind,PlotId=order.PlotId,PlacedAt=now,OrderId=order.Id,Scale=1};
        if(order.Operation=="demolish")
        {
            if(storage?.Goods.Any(g=>g.Amount>0)==true) throw new GameRuleException("building.emptyStorage","Empty this storage before demolition.");
            var residents=citizens.Where(c=>c.HomePlotId==order.PlotId).ToList();
            var otherBeds=village.Structures.Where(s=>s.PlotId!=order.PlotId&&!s.Decommissioning).Sum(ShelterRules.Beds);
            var already=citizens.Count(c=>c.HomePlotId!=null&&c.HomePlotId!=order.PlotId);
            if(otherBeds-already<residents.Count) throw new GameRuleException("housing.relocateFirst","Provide enough free beds and relocate the residents first.");
            foreach(var resident in residents) resident.HomePlotId=null;
            if(structure is not null) structure.Decommissioning=true;
            if(storage is not null) storage.Decommissioning=true;
            site.Operation="demolish";site.TargetLevel=0;
            site.OperationWork=Math.Max(3,(int)Math.Ceiling(BuildingCatalog.Get(kind).Stages.Sum(s=>s.Jobs.Where(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Labor).Sum(j=>j.Amount))*0.3));
        }
        else if(storage is not null&&StorageLimits.Upgrade(storage.Kind) is not null) site.Operation="storage-upgrade";
        else if(structure is {Kind:BuildingKind.Longhouse,Level:<3})
        {site.Operation="housing-upgrade";site.TargetLevel=structure.Level+1;}
        else throw new GameRuleException("building.maxLevel","This building cannot be upgraded further.");
        village.Sites.Add(site);order.Status="working";order.ApprovedAt=now;
        ShelterRules.Assign(village,citizens,now);
    }

    public static bool Finish(Village village,ConstructionSite site,DateTimeOffset now)
    {
        if(site.Operation is null)return false;
        var structure=village.Structures.SingleOrDefault(s=>s.PlotId==site.PlotId);
        var storage=village.Storages.SingleOrDefault(s=>s.PlotId==site.PlotId);
        if(site.Operation=="demolish")
        {
            if(storage?.Goods.Any(g=>g.Amount>0)==true)throw new GameRuleException("building.emptyStorage","Empty this storage first.");
            var investment=structure?.Investment??storage?.Investment??[];
            var returns=investment.Select(g=>new StoredGood{Kind=g.Kind,Amount=g.Amount/4}).Where(g=>g.Amount>0).ToList();
            // Simulate ALL refunds together: total-capacity bins are shared between resources.
            var bins=village.Storages.Where(s=>s.PlotId!=site.PlotId).Select(s=>new StorageBuilding
                {Kind=s.Kind,PlotId=s.PlotId,Decommissioning=s.Decommissioning,
                 Goods=s.Goods.Select(g=>new StoredGood{Kind=g.Kind,Amount=g.Amount}).ToList()}).ToList();
            foreach(var good in returns)
            {
                var remaining=good.Amount;
                foreach(var bin in bins) remaining-=bin.Put(good.Kind,remaining);
                if(remaining>0)throw new GameRuleException("building.returnSpace","Make room in storage for the recovered materials.");
            }
            if(structure is not null)village.Structures.Remove(structure);
            if(storage is not null)village.Storages.Remove(storage);
            foreach(var good in returns)village.Store(good.Kind,good.Amount);
        }
        else
        {
            if(site.Operation=="storage-upgrade"&&storage is not null)storage.Kind=StorageLimits.Upgrade(storage.Kind)!.Value;
            if(site.Operation=="housing-upgrade"&&structure is not null)structure.Level=site.TargetLevel;
            AddInvestment(site,structure?.Investment??storage!.Investment);
        }
        var order=village.CivicOrders.SingleOrDefault(o=>o.Id==site.OrderId);
        if(order is not null)order.Status="completed";
        village.Sites.Remove(site);return true;
    }

    public static void AddInvestment(ConstructionSite site,List<StoredGood> destination)
    {
        foreach(var progress in site.Progress)
        {
            var job=BuildingCatalog.FindJob(progress.JobId);
            if(job is not {Kind:JobKind.Haul,Resource:{} resource})continue;
            var existing=destination.SingleOrDefault(g=>g.Kind==resource);
            if(existing is null){existing=new StoredGood{Kind=resource};destination.Add(existing);}
            existing.Amount+=progress.Done;
        }
    }
}
