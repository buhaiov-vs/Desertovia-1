using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Farming;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public sealed class KingdomEconomyTests
{
    [Fact]
    public void Personal_transfers_and_private_warehouse_preserve_quantity_and_basis()
    {
        var village=TestWorld.NewVillage(); var a=TestWorld.NewCitizen(village); var b=TestWorld.NewCitizen(village);
        PersonalInventory.Add(a,ResourceKind.Wood,7,101);
        var basis=PersonalInventory.Remove(a,ResourceKind.Wood,3); PersonalInventory.Add(b,ResourceKind.Wood,3,basis);
        var store=StorageBuilding.Create(StorageKind.WoodPile,"-4,4",TestWorld.Now); store.OwnerAccountId=b.AccountId;
        PrivateWarehouse.Deposit(b,store,ResourceKind.Wood,3);
        Assert.Throws<GameRuleException>(()=>PrivateWarehouse.Withdraw(a,store,ResourceKind.Wood,1));
        Assert.Equal(3,PrivateWarehouse.Withdraw(b,store,ResourceKind.Wood,3));
        Assert.Equal(7,PersonalInventory.Amount(a,ResourceKind.Wood)+PersonalInventory.Amount(b,ResourceKind.Wood));
        Assert.Equal(101,a.PersonalGoods.Sum(g=>g.CostBasis)+b.PersonalGoods.Sum(g=>g.CostBasis));
        Assert.Empty(store.PersonalCosts); Assert.Equal(0,store.Amount(ResourceKind.Wood));
    }

    [Fact]
    public void Castle_upgrade_requires_more_material_and_labor_and_keeps_old_buildings()
    {
        var village=TestWorld.NewVillage(); var now=TestWorld.Now;
        var home=new Structure{Kind=BuildingKind.Hut,PlotId="2,2",BuiltAt=now}; village.Structures.Add(home);
        var castle=new Structure{Kind=BuildingKind.Castle,PlotId="18,14",BuiltAt=now};village.Structures.Add(castle);
        Assert.Equal(WorldMap.BuildArea,KingdomRules.Area(village));
        var order=BuildingOperations.Request(village,village.OwnerAccountId,castle.PlotId,"upgrade",now);
        BuildingOperations.Approve(village,order,[],now);
        var site=Assert.Single(village.Sites);
        var baseWork=BuildingCatalog.Get(BuildingKind.Castle).Stages.Sum(s=>s.Jobs.Where(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Labor).Sum(j=>j.Amount));
        Assert.Equal(baseWork*2,site.Definition.Stages.Sum(s=>s.Jobs.Where(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Labor).Sum(j=>j.Amount)));
        Assert.Equal(320,site.Required(1,"bring-stones"));
        Assert.True(BuildingOperations.Finish(village,site,now));
        Assert.Equal(2,castle.Level); Assert.True(KingdomRules.Area(village).Contains(WorldMap.Footprint(home.PlotId)!.Value));
        Assert.Contains(home,village.Structures); Assert.False(KingdomRules.Area(village).Contains(new TileArea(30,14,3,4)));
    }

    [Fact]
    public void Private_hauling_cannot_spend_common_stock_and_is_owner_only()
    {
        var village=TestWorld.NewVillage(); var owner=TestWorld.NewCitizen(village);var stranger=TestWorld.NewCitizen(village);
        var definition=BuildingCatalog.Get(BuildingKind.Hut);
        var stage=Enumerable.Range(0,definition.Stages.Count).First(i=>definition.Stages[i].Jobs.Any(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Haul));
        var job=definition.Stages[stage].Jobs.First(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Haul);
        var resource=BuildingCatalog.FindJob(job.JobId)!.Resource!.Value;
        var site=new ConstructionSite{Kind=BuildingKind.Hut,PlotId="-4,10",PlacedAt=TestWorld.Now,OwnerAccountId=owner.AccountId};village.Sites.Add(site);
        village.Store(resource,40);var common=village.Stock(resource);
        Assert.Throws<GameRuleException>(()=>ConstructionRules.CheckJob(village,owner,site.PlotId,stage,job.JobId,5));
        PersonalInventory.Add(owner,resource,5,15);
        Assert.Throws<GameRuleException>(()=>ConstructionRules.CheckJob(village,stranger,site.PlotId,stage,job.JobId,5));
        CycleEngine.StartJob(owner,site.PlotId,stage,job.JobId,WorkContext.For(village),TestWorld.Now);
        var report=CycleEngine.Settle(owner,WorkContext.For(village),owner.CycleEndsAt!.Value);
        Assert.NotNull(report);Assert.True(report.ProjectContribution>0);
        Assert.Equal(common,village.Stock(resource));Assert.True(PersonalInventory.Amount(owner,resource)<5);
    }

    [Fact]
    public void Private_farm_is_not_a_public_seedbed()
    {
        var village=TestWorld.NewVillage();var owner=TestWorld.NewCitizen(village);
        village.Structures.Add(new Structure{Kind=BuildingKind.Farm,PlotId="-4,10",OwnerAccountId=owner.AccountId,BuiltAt=TestWorld.Now});
        FarmRules.EnsureBeds(village);
        Assert.Null(FarmRules.EmptyBed(village));Assert.NotNull(FarmRules.EmptyBed(village,owner.AccountId));
        Assert.False(FarmRules.Sow(village,FarmCatalog.Crops[0].Kind,TestWorld.Now));
        Assert.True(FarmRules.Sow(village,FarmCatalog.Crops[0].Kind,TestWorld.Now,owner.AccountId));
    }

    [Fact]
    public void Private_blueprints_do_not_occupy_public_slots_or_raise_public_repeat_costs()
    {
        var village=TestWorld.NewVillage();Array.Fill(village.Explored,(byte)255);
        village.Structures.Add(new Structure{Kind=BuildingKind.LeanTo,PlotId="4,18",BuiltAt=TestWorld.Now});
        for(var i=0;i<4;i++)village.Sites.Add(new ConstructionSite{Kind=BuildingKind.Hut,PlotId=$"{-4-i*4},10",OwnerAccountId=Guid.NewGuid(),PlacedAt=TestWorld.Now});
        Assert.Equal(0,village.CountOf(BuildingKind.Hut));
        var site=ConstructionRules.Place(village,BuildingKind.Hut,8,10,TestWorld.Now);
        Assert.Null(site.OwnerAccountId);Assert.Equal(1,site.Scale);Assert.Equal(1,village.CountOf(BuildingKind.Hut));
    }

    [Theory]
    [InlineData(0,0)][InlineData(200,0)][InlineData(500,30)][InlineData(1000,130)][InlineData(1200,190)]
    public void Tax_preview_is_marginal(long income,long expected) => Assert.Equal(expected,EconomyRules.TaxPreview(income));
}
