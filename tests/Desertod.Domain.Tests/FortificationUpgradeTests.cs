using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;
namespace Desertod.Domain.Tests;

public sealed class FortificationUpgradeTests
{
    private static Village Fortified()
    {
        var v=NewVillage();v.Wilds=Wilderness.Generate(v.Id);v.Wilds.Obstacles.Clear();v.Wilds.Ravine.Clear();Fortifications.Ensure(v,[ProjectCatalog.Palisade]);
        v.Research.Add(new(){TechnologyId=TechnologyCatalog.Carpentry,ResearchedAt=Now});
        return v;
    }
    [Fact] public void Upgrade_is_real_queued_work_with_frozen_costs_and_no_instant_repair_or_resource_charge()
    {
        var v=Fortified();v.Fortifications[0].Damage=42;v.Fortifications[1].Damage=100;
        var quote=FortificationUpgrades.Quote(v);var wood=v.Stock(ResourceKind.Wood);
        var order=FortificationUpgrades.Request(v,v.OwnerAccountId,Now);FortificationUpgrades.Begin(v,order,Now);
        var site=Assert.Single(v.Sites);Assert.False(site.IsFinished);Assert.Equal(1,FortificationUpgrades.Level(v));
        Assert.Equal(wood,v.Stock(ResourceKind.Wood));Assert.Equal(42,v.Fortifications[0].Damage);
        Assert.Equal(quote.Materials.Single(g=>g.Resource==ResourceKind.Wood).Amount,site.Required(0,"bring-logs"));
        Assert.Equal(quote.Work,site.Required(0,"raise-posts"));Assert.Equal(WorldMap.Places[FortificationUpgrades.Plot],WorldMap.Yard(site.PlotId));
        site.Stage=site.Definition.Stages.Count;FortificationUpgrades.ApplyReady(v);
        Assert.Empty(v.Sites);Assert.Equal(2,FortificationUpgrades.Level(v));
        Assert.Equal(42,v.Fortifications[0].Damage);Assert.Equal(100,v.Fortifications[1].Damage);Assert.False(v.Fortifications[1].Blocks);
        Assert.Equal("completed",order.Status);FortificationUpgrades.ApplyReady(v);Assert.Equal(2,FortificationUpgrades.Level(v));
    }
    [Fact] public void A_newly_announced_wave_holds_the_paid_upgrade_until_all_waves_end_without_holding_the_worker()
    {
        var v=Fortified();var order=FortificationUpgrades.Request(v,v.OwnerAccountId,Now);FortificationUpgrades.Begin(v,order,Now);
        var site=Assert.Single(v.Sites);site.Stage=site.Definition.Stages.Count;
        var first=new Raid();var second=new Raid();v.Raids.AddRange([first,second]);
        FortificationUpgrades.ApplyReady(v);Assert.Single(v.Sites);Assert.Equal(1,FortificationUpgrades.Level(v));
        Assert.True(FortificationUpgrades.Quote(v).ReadyAfterBattle);
        first.Outcome=RaidOutcome.Repelled;FortificationUpgrades.ApplyReady(v);Assert.Single(v.Sites);
        second.Outcome=RaidOutcome.Cancelled;FortificationUpgrades.ApplyReady(v);Assert.Empty(v.Sites);Assert.Equal(2,FortificationUpgrades.Level(v));
    }
    [Fact] public void War_duplicate_requests_full_queue_and_missing_technology_cannot_be_bypassed()
    {
        var v=Fortified();v.Research.Clear();Assert.Equal("defense.upgradeTechnology",FortificationUpgrades.Quote(v).BlockedBy);
        v.Research.Add(new(){TechnologyId=TechnologyCatalog.Carpentry,ResearchedAt=Now});v.Raids.Add(new());
        Assert.Equal("defense.raidPending",Assert.Throws<GameRuleException>(()=>FortificationUpgrades.Request(v,v.OwnerAccountId,Now)).Code);
        v.Raids.Clear();
        for(var i=0;i<BuildingCatalog.MaximumOpenSites;i++)v.CivicOrders.Add(new(){PlotId=$"{i},0",Operation="build"});
        Assert.Equal("building.requestQueueFull",FortificationUpgrades.Quote(v).BlockedBy);
        v.CivicOrders.RemoveAt(0);var order=FortificationUpgrades.Request(v,v.OwnerAccountId,Now);
        Assert.Equal("building.orderExists",FortificationUpgrades.Quote(v).BlockedBy);
        FortificationUpgrades.Begin(v,order,Now);Assert.Single(v.Sites);
        Assert.Equal("defense.upgradeWorking",FortificationUpgrades.Quote(v).BlockedBy);
    }
    [Fact] public void Stone_requires_mining_and_is_the_final_tier()
    {
        var v=Fortified();foreach(var wall in v.Fortifications)wall.Level=2;
        Assert.Equal("defense.upgradeTechnology",FortificationUpgrades.Quote(v).BlockedBy);
        v.Research.Add(new(){TechnologyId=TechnologyCatalog.Mining,ResearchedAt=Now});
        var order=FortificationUpgrades.Request(v,v.OwnerAccountId,Now);FortificationUpgrades.Begin(v,order,Now);
        var site=Assert.Single(v.Sites);Assert.True(site.Required(0,"bring-stones")>0);site.Stage=1;FortificationUpgrades.ApplyReady(v);
        Assert.Equal(3,FortificationUpgrades.Level(v));Assert.All(v.Fortifications,f=>Assert.Equal(3.5,f.Strength));
        Assert.Equal("building.maxLevel",FortificationUpgrades.Quote(v).BlockedBy);
    }
    [Fact] public void Reinforcement_cannot_be_placed_as_a_fake_building_and_cancellation_keeps_paid_salvage()
    {
        var v=Fortified();Assert.False(BuildingCatalog.Get(BuildingKind.FortificationUpgrade).Buildable);
        Assert.Equal("building.notAvailable",Assert.Throws<GameRuleException>(()=>ConstructionRules.Place(v,BuildingKind.FortificationUpgrade,0,0,Now)).Code);
        var order=FortificationUpgrades.Request(v,v.OwnerAccountId,Now);FortificationUpgrades.Begin(v,order,Now);
        var site=Assert.Single(v.Sites);site.Record(0,"bring-logs",8,Guid.NewGuid());
        SiteManagement.BeginDismantle(v,site,v.OwnerAccountId,[]);FortificationUpgrades.ApplyReady(v);
        Assert.Equal(1,FortificationUpgrades.Level(v));Assert.Equal(2,Assert.Single(site.Salvage).Amount);
        site.Stage=site.Definition.Stages.Count;Assert.Null(ConstructionRules.Advance(v,site,1,Now));
        Assert.Equal(2,Assert.Single(Assert.Single(v.SiteRecoveries).Goods).Amount);Assert.Empty(v.Sites);
    }
    [Fact] public void Stronger_walls_and_gates_take_less_damage_from_the_same_actual_raider()
    {
        double Damage(int level)
        {
            var v=Fortified();foreach(var f in v.Fortifications){f.Level=level;if(f.Gate)f.Open=false;}
            var raid=new Raid{Enemy=EnemyKind.Bear,StrikesAt=Now};v.Raids.Add(raid);
            var b=new RaidBattle{Id=raid.Id,VillageId=v.Id,Sector="west",Tactic="breach",Phase="Approaching",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(4),
                Units=[new(){X=-1.5,Y=10.5,SpawnX=-1.5,SpawnY=10.5,Health=100,MaximumHealth=100,Attack=1,Speed=1}]};
            for(var i=1;i<=5;i++)RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(i));
            return v.Fortifications.Sum(f=>f.Damage);
        }
        var wood=Damage(1);var stone=Damage(3);Assert.True(wood>0);Assert.True(stone>0);Assert.True(stone<wood);
    }
    [Fact] public void Repair_is_paid_and_preserves_the_wall_material_and_gate_state()
    {
        var v=Fortified();var wall=v.Fortifications.First(f=>f.Gate);wall.Open=false;wall.Level=3;wall.Damage=70;
        var c=NewCitizen(v);var context=WorkContext.For(v);
        Assert.Equal("storage.missingInput",Assert.Throws<GameRuleException>(()=>CycleEngine.Start(c,Task("repair"),context,Now,workplacePlotId:wall.Id)).Code);
        v.Store(ResourceKind.Wood,2);v.Store(ResourceKind.Stone,1);
        CycleEngine.Start(c,Task("repair"),context,Now,workplacePlotId:wall.Id);Finish(c,context);
        Assert.Equal(60,wall.Damage);Assert.Equal(3,wall.Level);Assert.False(wall.Open);
        Assert.Equal(0,v.Stock(ResourceKind.Wood));Assert.Equal(0,v.Stock(ResourceKind.Stone));
    }
}
