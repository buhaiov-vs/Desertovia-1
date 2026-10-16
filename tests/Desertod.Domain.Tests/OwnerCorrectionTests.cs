using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Projects;
using Desertod.Domain.Stats;
using Desertod.Domain.Technologies;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;
namespace Desertod.Domain.Tests;

public sealed class OwnerCorrectionTests
{
    [Fact]
    public void Heal_is_atomic_capped_and_old_receipts_never_charge_twice()
    {
        var v=NewVillage();var c=NewCitizen(v);PersonalInventory.Add(c,ResourceKind.Remedy,3,0);
        var f=new RaidFighter{CitizenId=c.Id,Active=true,Health=10,MaximumHealth=40};var first=Guid.NewGuid();
        Assert.True(BattleActions.Heal(v,c,f,first,Now));Assert.Equal(18,f.Health);Assert.Equal(2,PersonalInventory.Amount(c,ResourceKind.Remedy));
        Assert.False(BattleActions.Heal(v,c,f,first,Now));Assert.Equal(2,PersonalInventory.Amount(c,ResourceKind.Remedy));
        Assert.Equal("battle.healCooldown",Assert.Throws<GameRuleException>(()=>BattleActions.Heal(v,c,f,Guid.NewGuid(),Now)).Code);
        BattleActions.Heal(v,c,f,Guid.NewGuid(),Now.AddMinutes(1));
        Assert.False(BattleActions.Heal(v,c,f,first,Now.AddMinutes(2)));Assert.Equal(26,f.Health);Assert.Equal(1,PersonalInventory.Amount(c,ResourceKind.Remedy));
        f.Health=39;BattleActions.Heal(v,c,f,Guid.NewGuid(),Now.AddMinutes(3));Assert.Equal(40,f.Health);
    }
    [Fact]
    public void Missing_remedy_and_full_health_do_not_change_health_cooldown_or_inventory()
    {
        var v=NewVillage();var f=new RaidFighter{Active=true,Health=10,MaximumHealth=40};
        Assert.Equal("heal.none",Assert.Throws<GameRuleException>(()=>BattleActions.Heal(v,null,f,Guid.NewGuid(),Now)).Code);
        Assert.Equal(10,f.Health);Assert.Null(f.HealReadyAt);
        f.Health=40;Assert.Equal("battle.fullHealth",Assert.Throws<GameRuleException>(()=>BattleActions.Heal(v,null,f,Guid.NewGuid(),Now)).Code);
    }
    [Fact]
    public void Retreat_physically_moves_then_withdraws_without_restoring_health()
    {
        var v=NewVillage();var nav=new RaidNavigation(v);var p=nav.NearestFree(new(20.5,13.5));
        var b=new RaidBattle{Sector="north"};var f=new RaidFighter{Active=true,Guard=true,X=p.X,Y=p.Y,Health=17,MaximumHealth=40};
        BattleActions.Retreat(b,f,nav);Assert.Equal(p,f.Position);Assert.Equal("retreating",f.Mode);
        for(var i=0;i<100&&f.Active;i++)BattleActions.StepRetreat(f,nav);
        Assert.False(f.Active);Assert.Equal("withdrawn",f.Mode);Assert.Equal(17,f.Health);
    }
    [Fact]
    public void Withdrawn_fighter_is_not_automatically_recruited_by_the_next_tick()
    {
        var v=NewVillage();var c=NewCitizen(v);c.IdleSpot=new(15.5,13.5);
        var raid=new Raid{Source=RaidSource.Undead,Enemy=EnemyKind.Zombie,Count=1,StrikesAt=Now,AnnouncedAt=Now};
        var f=new RaidFighter{CitizenId=c.Id,Mode="withdrawn",Health=15,MaximumHealth=40};
        var b=new RaidBattle{Sector="north",Phase="Fighting",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(4),Fighters=[f],Units=[new(){Health=100,MaximumHealth=100,X=15.5,Y=13.5,Speed=0,Attack=1}]};
        RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(1));
        Assert.False(f.Active);Assert.Equal("withdrawn",f.Mode);Assert.Equal(15,f.Health);
    }
    [Fact]
    public void Relocation_does_not_treat_another_owners_private_beds_as_free_public_housing()
    {
        var v=NewVillage();var resident=NewCitizen(v);resident.HomePlotId="1,1";
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="1,1"});
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="5,1",OwnerAccountId=Guid.NewGuid()});
        var error=Assert.Throws<GameRuleException>(()=>ShelterRules.RequireRelocation(v,[resident],"1,1"));
        Assert.Equal("housing.relocateFirst",error.Code);Assert.Equal("1,1",resident.HomePlotId);
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="9,1",Damage=100});
        Assert.Empty(ShelterRules.Relocation(v,[resident],"1,1"));
    }
    [Fact]
    public void Successful_demolition_assigns_specific_suitable_beds_instead_of_clearing_addresses()
    {
        var v=NewVillage();var people=Enumerable.Range(0,4).Select(_=>NewCitizen(v)).ToArray();foreach(var c in people)c.HomePlotId="1,1";
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="1,1"});v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="5,1"});
        var o=new CivicOrder{PlotId="1,1",Operation="demolish",RequestedBy=v.OwnerAccountId};
        BuildingOperations.Approve(v,o,people,Now);
        Assert.All(people,c=>Assert.Equal("5,1",c.HomePlotId));Assert.Equal("working",o.Status);
    }
    [Theory]
    [InlineData(BuildingKind.Hut)]
    [InlineData(BuildingKind.Workshop)]
    [InlineData(BuildingKind.Hospital)]
    [InlineData(BuildingKind.Castle)]
    public void Ruins_remain_nonfunctional_until_full_paid_repair_progress_is_complete(BuildingKind kind)
    {
        var v=NewVillage();var b=new Structure{Kind=kind,PlotId="1,1",Level=2};v.Structures.Add(b);
        CombatRules.Damage(v,b.PlotId,100);RuinRules.Collapse(v,b.PlotId,[],Now,Guid.NewGuid());
        Assert.NotNull(b.RuinedAt);Assert.False(v.Has(kind));
        CombatRules.Repair(v,5);Assert.NotNull(b.RuinedAt);Assert.False(v.Has(kind));
        for(var i=0;i<19;i++)CombatRules.Repair(v,5);
        Assert.Null(b.RuinedAt);Assert.True(v.Has(kind));Assert.Equal(2,b.Level);Assert.Null(v.DefeatedAt);
    }
    [Fact]
    public void Destroyed_storage_keeps_goods_but_does_not_supply_stock_or_capacity()
    {
        var v=NewVillage();var s=StorageBuilding.Create(StorageKind.WoodPile,"1,1",Now);s.Put(ResourceKind.Wood,7);v.Storages.Add(s);
        CombatRules.Damage(v,s.PlotId,100);RuinRules.Collapse(v,s.PlotId,[],Now,Guid.NewGuid());
        Assert.Equal(7,s.Amount(ResourceKind.Wood));Assert.Equal(0,v.Stock(ResourceKind.Wood));Assert.Equal(0,s.FreeSpace(ResourceKind.Wood));
        for(var i=0;i<20;i++)CombatRules.Repair(v,5);
        Assert.Equal(7,v.Stock(ResourceKind.Wood));
    }
    [Fact]
    public void Collapse_releases_site_claims_without_erasing_previous_contributions_or_load()
    {
        var v=NewVillage();var c=NewCitizen(v);var site=new ConstructionSite{Kind=BuildingKind.Hut,PlotId="1,1"};v.Sites.Add(site);
        site.Record(0,"bring-logs",3,c.Id);c.CurrentSiteId=site.PlotId;c.CycleSettled=false;c.ReservedSiteWork=1;
        CombatRules.Damage(v,site.PlotId,100);RuinRules.Collapse(v,site.PlotId,[c],Now,Guid.NewGuid());
        Assert.Equal(3,site.Done(0,"bring-logs"));Assert.NotNull(site.RuinedAt);Assert.True(c.CycleSettled);Assert.Null(c.CurrentSiteId);Assert.Null(c.ReservedSiteWork);
        Assert.Contains(CombatRules.Buildings(v),x=>x.PlotId==site.PlotId&&x.Damage==100);
    }
    [Fact]
    public void Undead_can_damage_an_unfinished_site_using_the_actual_attack_simulation()
    {
        var v=NewVillage();var site=new ConstructionSite{Kind=BuildingKind.Hut,PlotId="14,18"};v.Sites.Add(site);
        var yard=WorldMap.Yard(site.PlotId);var raid=new Raid{Source=RaidSource.Undead,Enemy=EnemyKind.Zombie,Count=4,StrikesAt=Now,AnnouncedAt=Now};
        var b=new RaidBattle{Sector="north",Phase="Fighting",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(4),Units=Enumerable.Range(0,4).Select(_=>new RaidUnit{X=yard.X,Y=yard.Y,Health=100,MaximumHealth=100,Speed=0}).ToList()};
        Assert.True(RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(1)));
        Assert.True(site.Damage>0);Assert.Contains(b.Damage,x=>x.PlotId==site.PlotId);
    }
    [Fact]
    public void New_kingdom_foundation_is_explicit_idempotent_and_never_applied_by_loading_old_worlds()
    {
        var v=NewVillage();v.EnsureCamp(Now);Assert.DoesNotContain(v.Structures,s=>s.Kind==BuildingKind.Castle);
        KingdomRules.FoundCastle(v,Now);KingdomRules.FoundCastle(v,Now);
        var castle=Assert.Single(v.Structures,s=>s.Kind==BuildingKind.Castle);Assert.Equal("14,9",castle.PlotId);Assert.Equal(1,castle.Level);
    }
    [Fact]
    public void Royal_bridge_destruction_closes_far_bank_until_complete_repair_without_forgetting_its_project()
    {
        var v=NewVillage();v.BridgeOpened=true;
        CombatRules.Damage(v,RuinRules.RoyalBridge,100);RuinRules.Collapse(v,RuinRules.RoyalBridge,[],Now,Guid.NewGuid());
        Assert.False(v.BridgeOpened);Assert.NotNull(v.BridgeRuinedAt);Assert.False(BankAccess.CanReach(v,new(40,12)));
        CombatRules.Repair(v,5);Assert.False(v.BridgeOpened);
        for(var i=0;i<19;i++)CombatRules.Repair(v,5);
        Assert.True(v.BridgeOpened);Assert.Null(v.BridgeRuinedAt);Assert.Equal(0,v.BridgeDamage);
    }

    [Fact]
    public void Destroying_an_already_damaged_castle_is_a_defeat_even_when_this_raid_did_little_damage()
    {
        var v=NewVillage();var castle=new Structure{Kind=BuildingKind.Castle,PlotId="14,18",Damage=99};v.Structures.Add(castle);
        var yard=WorldMap.Yard(castle.PlotId);var raid=new Raid{Source=RaidSource.Undead,Enemy=EnemyKind.Zombie,Count=1,StrikesAt=Now,AnnouncedAt=Now};
        var b=new RaidBattle{Sector="north",Phase="Fighting",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(4),
            Units=[new(){X=yard.X,Y=yard.Y,Health=100,MaximumHealth=100,Speed=0}]};
        Assert.True(RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(1)));
        Assert.NotNull(v.DefeatedAt);Assert.NotNull(b.FinishedAt);Assert.Equal(RaidOutcome.Lost,raid.Outcome);
        Assert.Equal(0,raid.Killed);Assert.Equal(100,b.Units[0].Health);Assert.Equal("Dispersed",b.Units[0].State);
    }

    [Fact]
    public void Completed_project_knowledge_cannot_reopen_a_ruined_bridge_when_starting_work()
    {
        var v=NewVillage();v.BridgeOpened=true;
        CombatRules.Damage(v,RuinRules.RoyalBridge,100);RuinRules.Collapse(v,RuinRules.RoyalBridge,[],Now,Guid.NewGuid());
        CycleEngine.Start(NewCitizen(v),Task("forage"),new WorkContext{Village=v,CompletedProjects=new HashSet<string>{ProjectCatalog.TsarBridge}},Now);
        Assert.False(v.BridgeOpened);Assert.NotNull(v.BridgeRuinedAt);Assert.False(BankAccess.CanReach(v,new(40,12)));
    }
    [Fact]
    public void A_ruined_workshop_does_not_disable_another_intact_public_workshop()
    {
        var v=NewVillage();var ruined=new Structure{Kind=BuildingKind.Workshop,PlotId="1,1",Damage=100,RuinedAt=Now};
        var working=new Structure{Kind=BuildingKind.Workshop,PlotId="21,12"};v.Structures.AddRange([ruined,working]);
        v.Research.Add(new(){TechnologyId=TechnologyCatalog.Carpentry,ResearchedAt=Now});v.Store(ResourceKind.Wood,10);
        var c=NewCitizen(v,(Stat.Dexterity,4),(Stat.Strength,2));CycleEngine.Start(c,Task("saw-planks"),WorkContext.For(v),Now);
        Assert.Equal("saw-planks",c.CurrentTaskId);Assert.Equal(WorldMap.Yard(working.PlotId),CycleEngine.Workplace(v,Task("saw-planks")));
    }

}
