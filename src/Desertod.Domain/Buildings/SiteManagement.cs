using Desertod.Domain.Entities;
using Desertod.Domain.Storage;
using Desertod.Domain.World;

namespace Desertod.Domain.Buildings;

/// <summary>Durable salvage belongs to its original owner until explicitly collected.</summary>
public sealed class SiteRecovery
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string PlotId { get; set; }
    public Guid? OwnerAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<StoredGood> Goods { get; set; } = [];
}

public static class SiteManagement
{
    // A later castle expansion must not confiscate a formerly legal private plot.
    public static bool LegacyConflict(ConstructionSite site) => !site.BoundaryApproved && site.OwnerAccountId is not null
        && WorldMap.Footprint(site.PlotId) is {} area && WorldMap.BuildArea.Overlaps(area);

    public static void BeginDismantle(Village village, ConstructionSite site, Guid actor, IReadOnlyList<Citizen> people)
    {
        var tsar = actor == village.OwnerAccountId;
        if (!(site.OwnerAccountId==actor || site.OwnerAccountId is null && tsar || tsar && LegacyConflict(site)))
            throw new GameRuleException("building.private","Only the owner or the Tsar of a conflicting plot can dismantle this site.");
        if (site.Operation=="site-demolish") return;
        if (site.Operation is not null&&site.Operation!=Desertod.Domain.Combat.FortificationUpgrades.Operation) throw new GameRuleException("building.busy","Finish the current building operation first.");
        var investment=new List<StoredGood>();BuildingOperations.AddInvestment(site,investment);
        site.Salvage=investment.Where(g=>g.Amount>=4).Select(g=>new StoredGood{Kind=g.Kind,Amount=g.Amount/4}).ToList();
        site.OperationWork=Math.Max(3,(int)Math.Ceiling(site.Progress
            .Where(p=>BuildingCatalog.FindJob(p.JobId)?.Kind==JobKind.Labor).Sum(p=>p.Done)*.3));
        foreach(var person in people.Where(c=>c.CurrentSiteId==site.PlotId))
        {
            // No healing, stat reset or cancellation of unrelated work. Hauling consumes at settlement.
            person.CurrentSiteId=null;person.CurrentSiteStage=null;person.ReservedSiteWork=null;
            person.CurrentTaskId=null;person.CurrentTechnologyId=null;person.CycleSettled=true;
            person.CycleEndsAt=null;person.CycleStartedAt=null;person.Spot=null;
        }
        site.PublicDemolition=site.OwnerAccountId is null || tsar;
        site.RuinedAt=null;site.Damage=0;
        site.Operation="site-demolish";site.Stage=0;site.Progress.Clear();site.Hands.Clear();site.TreesToFell=0;
    }
}
