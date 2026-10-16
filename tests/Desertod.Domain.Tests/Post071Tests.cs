using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class Post071Tests
{
    private static (Village Village,Raid Raid,RaidBattle Battle) Encounter()
    {
        var v=NewVillage();
        var r=new Raid{Source=RaidSource.Undead,Enemy=EnemyKind.Zombie,EngineVersion=2,Count=2,AnnouncedAt=Now,StrikesAt=Now};v.Raids.Add(r);
        return(v,r,new RaidBattle{Id=r.Id,VillageId=v.Id,Sector="north",Phase="Fighting",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(4),
            Units=[new(){X=15.5,Y=13.5,Health=30,MaximumHealth=30,Attack=3,Speed=0,NextAttackAt=Now},
                   new(){X=15.5,Y=14.5,Health=30,MaximumHealth=30,Attack=3,Speed=0,NextAttackAt=Now}]});
    }
    [Fact]
    public void Cancel_is_terminal_preserves_existing_damage_and_cannot_pay_twice()
    {
        var(v,r,b)=Encounter();var person=NewCitizen(v);person.InjuredUntil=Now.AddHours(1);
        b.Units[0].Health=0;b.DamageDone=17;var morale=v.MoraleAt(Now);
        RaidSimulation.Cancel(v,r,b,[person],Now);
        var stock=v.Stock(ResourceKind.Food);var loot=r.Loot.Sum(g=>g.Amount);var revision=b.Revision;
        Assert.Equal(RaidOutcome.Cancelled,r.Outcome);Assert.Equal("Cancelled",b.Phase);
        Assert.Equal(1,r.Killed);Assert.All(b.Units,u=>Assert.False(u.Alive));
        Assert.Equal(17,b.DamageDone);Assert.Equal(Now.AddHours(1),person.InjuredUntil);Assert.Equal(morale,v.MoraleAt(Now));
        RaidSimulation.Cancel(v,r,b,[person],Now.AddSeconds(1));
        Assert.False(RaidSimulation.Tick(v,r,b,[person],new HashSet<Guid>(),Now.AddSeconds(2)));
        Assert.Equal(revision,b.Revision);Assert.Equal(stock,v.Stock(ResourceKind.Food));Assert.Equal(loot,r.Loot.Sum(g=>g.Amount));
    }
    [Fact]
    public void Tsar_uses_own_account_and_actual_range_and_cooldown()
    {
        var(v,r,b)=Encounter();
        var fighter=new RaidFighter{IsTsar=true,AccountId=v.OwnerAccountId,X=15.5,Y=13.5,Health=80,MaximumHealth=80,Guard=true,Active=true,TargetUnitId=b.Units[0].Id,NextAttackAt=Now};b.Fighters.Add(fighter);
        RaidSimulation.Tick(v,r,b,[],new HashSet<Guid>(),Now.AddSeconds(1));
        Assert.Equal(22,b.Units[0].Health);Assert.Equal(30,b.Units[1].Health);Assert.Equal(8,fighter.DamageDealt);
        Assert.Equal(v.OwnerAccountId,fighter.ActorId);Assert.Equal(Guid.Empty,fighter.CitizenId);
        Assert.False(RaidSimulation.Tick(v,r,b,[],new HashSet<Guid>(),Now.AddSeconds(1)));
        Assert.Equal(22,b.Units[0].Health);
    }
    [Fact]
    public void Tsar_can_be_wounded_and_cannot_attack_during_recovery()
    {
        var(v,r,b)=Encounter();b.Units[0].Attack=200;
        var fighter=new RaidFighter{IsTsar=true,AccountId=v.OwnerAccountId,X=15.5,Y=13.5,Health=80,MaximumHealth=80,Guard=true,Active=true,NextAttackAt=Now};b.Fighters.Add(fighter);
        RaidSimulation.Tick(v,r,b,[],new HashSet<Guid>(),Now.AddSeconds(1));
        Assert.True(fighter.Wounded);Assert.Equal(0,fighter.Health);Assert.True(v.TsarInjuredUntil>Now);
        Assert.Equal(RescueRules.Ward(v),v.TsarSpot);
        var dealt=fighter.DamageDealt;RaidSimulation.Tick(v,r,b,[],new HashSet<Guid>(),Now.AddSeconds(2));Assert.Equal(dealt,fighter.DamageDealt);
    }
    [Fact]
    public void Wrong_account_tsar_actor_cannot_attack()
    {
        var(v,r,b)=Encounter();
        b.Fighters.Add(new(){IsTsar=true,AccountId=Guid.NewGuid(),X=15.5,Y=13.5,Health=80,Guard=true,Active=true});
        RaidSimulation.Tick(v,r,b,[],new HashSet<Guid>(),Now.AddSeconds(1));Assert.Equal(30,b.Units[0].Health);
    }
    [Fact]
    public void River_end_caps_cover_dry_shore_without_repairing_old_walls()
    {
        for(var sample=0;sample<8;sample++)
        {
            var v=NewVillage();Fortifications.Ensure(v,[ProjectCatalog.Palisade]);
            var old=v.Fortifications.Single(f=>f.X==5&&f.Y==-1);old.Damage=100;
            foreach(var gate in v.Fortifications.Where(f=>f.Gate))gate.Open=false;
            Fortifications.Ensure(v,[ProjectCatalog.Palisade]);Assert.Equal(100,old.Damage);
            var nav=new RaidNavigation(v);var seed=Wilderness.Seed(v.Id.ToString());
            foreach(var y in new[]{-1,26})for(var x=28;x<Wilderness.RiverWestBank(y,seed);x++)
                if(nav.Dry(x,y))Assert.Contains(v.Fortifications,f=>f.X==x&&f.Y==y&&f.Blocks);
            Assert.All(v.Fortifications.Where(f=>f.Gate),f=>Assert.False(f.Open));
        }
    }
    [Fact]
    public void Dismantling_private_site_keeps_refund_for_original_owner_without_healing_workers()
    {
        var v=NewVillage();var owner=NewCitizen(v);owner.CurrentSiteId="26,5";owner.CurrentTaskId="bring-logs";owner.CycleSettled=false;
        owner.ReservedSiteWork=4;owner.InjuredUntil=Now.AddHours(1);owner.RecoveryPausedAt=Now;
        var site=new ConstructionSite{Kind=BuildingKind.Hut,PlotId="26,5",OwnerAccountId=owner.AccountId,PlacedAt=Now,
            Progress=[new(){JobId="bring-logs",Stage=2,Done=12},new(){JobId="bring-stones",Stage=1,Done=8}]};v.Sites.Add(site);
        Assert.True(SiteManagement.LegacyConflict(site));
        SiteManagement.BeginDismantle(v,site,v.OwnerAccountId,[owner]);
        Assert.Equal("site-demolish",site.Operation);Assert.True(site.PublicDemolition);Assert.Null(owner.CurrentSiteId);
        Assert.Equal(Now.AddHours(1),owner.InjuredUntil);Assert.Equal(Now,owner.RecoveryPausedAt);
        Assert.Equal(3,site.Salvage.Single(g=>g.Kind==ResourceKind.Wood).Amount);
        site.Record(0,"dismantle",site.OperationWork,Guid.NewGuid());
        ConstructionRules.Advance(v,site,1,Now,NewCitizen(v));
        Assert.DoesNotContain(site,v.Sites);var recovery=Assert.Single(v.SiteRecoveries);
        Assert.Equal(owner.AccountId,recovery.OwnerAccountId);Assert.Equal(2,recovery.Goods.Single(g=>g.Kind==ResourceKind.Stone).Amount);
    }
    [Fact]
    public void Tsar_cannot_confiscate_a_legal_outside_site_or_a_private_site_approved_inside()
    {
        var v=NewVillage();var owner=NewCitizen(v);var site=new ConstructionSite{Kind=BuildingKind.Hut,PlotId="-8,27",OwnerAccountId=owner.AccountId,PlacedAt=Now};
        Assert.Throws<GameRuleException>(()=>SiteManagement.BeginDismantle(v,site,v.OwnerAccountId,[]));
        site.PlotId="26,5";site.BoundaryApproved=true;
        Assert.Throws<GameRuleException>(()=>SiteManagement.BeginDismantle(v,site,v.OwnerAccountId,[]));
        SiteManagement.BeginDismantle(v,site,owner.AccountId,[]);Assert.False(site.PublicDemolition);
    }
}
