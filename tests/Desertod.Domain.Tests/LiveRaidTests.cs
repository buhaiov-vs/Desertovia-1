using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Chat;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class LiveRaidTests
{
    private static Citizen Guard(Village village, int strength=3)
    {
        var c=NewCitizen(village,(Stat.Strength,strength),(Stat.Endurance,3));
        c.CurrentTaskId=GameCatalog.Tasks.First(t=>t.Effect==TaskEffect.Guard).Id;
        c.CycleSettled=false;c.CycleStartedAt=Now;c.CycleEndsAt=Now.AddHours(1);
        return c;
    }
    private static (Raid Raid,RaidBattle Battle) Battle(Village v,EnemyKind enemy=EnemyKind.Zombie)
    {
        var raid=new Raid{Enemy=enemy,AnnouncedAt=Now,StrikesAt=Now,Count=2,EngineVersion=2};
        v.Raids.Add(raid);
        return(raid,new(){Id=raid.Id,VillageId=v.Id,Sector="west",Phase="Fighting",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(4),
            Units=[new(){X=14.5,Y=13.5,SpawnX=-5.5,SpawnY=13.5,Health=15,MaximumHealth=15,Attack=3,Speed=.6,NextAttackAt=Now},
                   new(){X=13.5,Y=13.5,SpawnX=-6.5,SpawnY=13.5,Health=20,MaximumHealth=20,Attack=3,Speed=.6,NextAttackAt=Now}]});
    }
    [Theory]
    [InlineData("north")][InlineData("west")][InlineData("south")]
    public void Announcement_freezes_one_external_side_and_distinct_units(string sector)
    {
        var v=NewVillage();var c=Guard(v);
        var raid=new Raid{Enemy=EnemyKind.Skeleton,StrikesAt=Now.AddMinutes(3)};
        var b=RaidBalance.Prepare(v,raid,[c],[],new HashSet<Guid>{c.Id},Now,sector,"swarm");
        var nav=new RaidNavigation(v);
        Assert.Equal(sector,b.Sector);Assert.InRange(b.Units.Count,1,RaidBalance.MaximumUnits);
        Assert.All(b.Units,u=>{Assert.True(nav.IsOutside(u.Position,sector));Assert.True(nav.Pass((int)Math.Floor(u.X),(int)Math.Floor(u.Y)));});
        Assert.Equal(b.Units.Count,b.Units.Select(u=>(u.X,u.Y)).Distinct().Count());
        var initial=b.Units.Select(u=>(u.Id,u.Health,u.X,u.Y)).ToArray();var budget=b.ThreatBudget;
        c.Stats.Set(Stat.Strength,103);
        Assert.False(RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>{c.Id},raid.StrikesAt.AddMilliseconds(-1)));
        Assert.Equal(initial,b.Units.Select(u=>(u.Id,u.Health,u.X,u.Y)).ToArray());
        Assert.Equal(budget,b.ThreatBudget);Assert.Null(raid.ResolvedAt);
    }
    [Fact]
    public void Profiles_trade_number_for_strength_without_exceeding_the_same_budget()
    {
        const double budget=80;
        Assert.True(RaidBalance.Count(EnemyKind.Skeleton,"swarm",budget)>RaidBalance.Count(EnemyKind.Skeleton,"elite",budget));
        foreach(var profile in new[]{"swarm","elite","mixed"})
        {
            var v=NewVillage();var c=Guard(v,10);var raid=new Raid{Enemy=EnemyKind.Skeleton,StrikesAt=Now.AddMinutes(3)};
            var b=RaidBalance.Prepare(v,raid,[c],[],new HashSet<Guid>{c.Id},Now,"west",profile);
            Assert.True(b.Units.Sum(u=>EnemyCatalog.Get(raid.Enemy).Power*Math.Pow(RaidBalance.Factor(u.Variant),2))<=b.ThreatBudget+.001);
            Assert.Equal(RaidBalance.Count(raid.Enemy,profile,b.ThreatBudget),b.Units.Count);
        }
    }
    [Fact]
    public void A_hit_and_death_belong_to_one_enemy_and_repeat_tick_does_not_add_damage()
    {
        var v=NewVillage();var c=Guard(v,30);var (raid,b)=Battle(v);
        b.Fighters.Add(new(){CitizenId=c.Id,X=15.5,Y=13.5,Health=44,MaximumHealth=44,Guard=true,Active=true,NextAttackAt=Now});
        RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(1));
        Assert.Equal(0,b.Units[0].Health);Assert.Equal(20,b.Units[1].Health);
        Assert.Equal(1,b.Fighters[0].Kills);Assert.Null(raid.ResolvedAt);
        var hp=b.Units.Select(u=>u.Health).ToArray();var revision=b.Revision;
        Assert.False(RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(1)));
        Assert.Equal(hp,b.Units.Select(u=>u.Health).ToArray());Assert.Equal(revision,b.Revision);
    }
    [Fact]
    public void Gates_and_breaches_are_real_navigation_and_line_of_sight_barriers()
    {
        var v=NewVillage();
        // Fixed terrain: random village IDs can place a pond outside this gate and test water instead of walls.
        v.Id=Guid.Parse("00000000-0000-0000-0000-000000000701");
        Fortifications.Ensure(v,[ProjectCatalog.Palisade]);
        foreach(var gate in v.Fortifications.Where(f=>f.Gate))gate.Open=false;
        var nav=new RaidNavigation(v);var outside=new TilePoint(-2.5,13.5);var inside=new TilePoint(.5,13.5);
        Assert.False(nav.ClearShot(outside,inside));
        Assert.False(nav.Pass(-1,13));
        foreach(var gate in v.Fortifications.Where(f=>f.Gate))gate.Open=true;
        Assert.True(nav.ClearShot(outside,inside));
        var wall=v.Fortifications.Single(f=>f.X==-1&&f.Y==9);
        Assert.Equal(new TilePoint(.5,9.5),CombatRules.RepairYard(v,wall.Id));
        Assert.False(nav.Pass(-1,9));wall.Damage=100;Assert.True(nav.Pass(-1,9));
        CombatRules.Repair(v,100);Assert.False(nav.Pass(-1,9));
    }
    [Fact]
    public void Guard_cannot_damage_an_enemy_through_a_closed_gate()
    {
        var v=NewVillage();var c=Guard(v,20);c.DefenseSector="west";c.Weapon=ResourceKind.Bow;c.Arrows=10;
        Fortifications.Ensure(v,[ProjectCatalog.Palisade]);foreach(var gate in v.Fortifications.Where(f=>f.Gate))gate.Open=false;
        var (raid,b)=Battle(v);b.Units.RemoveAt(1);b.Units[0].X=-1.5;b.Units[0].Y=13.5;b.Units[0].Speed=0;
        b.Fighters.Add(new(){CitizenId=c.Id,X=.5,Y=13.5,Health=44,MaximumHealth=44,Guard=true,Active=true,NextAttackAt=Now});
        var health=b.Units[0].Health;
        RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(1));
        Assert.Equal(health,b.Units[0].Health);
    }
    [Fact]
    public void Server_outage_freezes_the_fight_instead_of_resolving_unseen_hours()
    {
        var v=NewVillage();var (raid,b)=Battle(v);var p=b.Units[0].Position;
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddHours(4));
        Assert.Null(b.FinishedAt);Assert.Null(raid.ResolvedAt);
        Assert.Equal(Now.AddHours(4).AddMinutes(4).AddSeconds(-1),b.EndsAt);
        Assert.InRange(p.DistanceTo(b.Units[0].Position),0,.61);
        Assert.Contains(b.Events,e=>e.Kind=="resumed");
    }
    [Fact]
    public void Wounds_pause_paid_work_and_healing_resumes_remaining_time_once()
    {
        var v=NewVillage();var c=NewCitizen(v);var context=new WorkContext{Village=v};
        CycleEngine.Start(c,Task("forage"),context,Now);
        var end=c.CycleEndsAt;var energy=c.StoredEnergy;var reservation=c.ReservedSiteWork;
        var hit=Now.AddSeconds(1);CombatRules.Injure(c,TimeSpan.FromMinutes(10),hit);
        Assert.Equal(WorkState.Paused,c.WorkState(hit));
        Assert.Null(CycleEngine.Settle(c,context,end!.Value.AddMinutes(1)));
        Assert.False(c.CycleSettled);
        v.Store(ResourceKind.Remedy,1);var healed=hit.AddMinutes(2);CombatRules.Heal(c,v,healed);
        Assert.Equal(end+TimeSpan.FromMinutes(2),c.CycleEndsAt);Assert.Equal(energy,c.StoredEnergy);
        Assert.Equal(reservation,c.ReservedSiteWork);
        Assert.False(HospitalRules.Resume(c,healed));
        Assert.NotNull(CycleEngine.Settle(c,context,c.CycleEndsAt!.Value));
        Assert.Null(CycleEngine.Settle(c,context,c.CycleEndsAt.Value));
    }
    [Fact]
    public void Overlapping_native_and_external_stays_have_one_pause_not_two()
    {
        var v=NewVillage();var c=Guard(v);var end=c.CycleEndsAt;
        CombatRules.Injure(c,TimeSpan.FromMinutes(10),Now);
        HospitalRules.Apply(c,new(){Available=true,AdmittedAt=Now.AddMinutes(2),Until=Now.AddMinutes(20)},Now.AddMinutes(2));
        HospitalRules.Apply(c,new(){Available=true,Until=null},Now.AddMinutes(21));
        Assert.Null(c.RecoveryPausedAt);Assert.Equal(end+TimeSpan.FromMinutes(20),c.CycleEndsAt);
    }
    [Fact]
    public void No_resources_leave_storage_until_a_live_carrier_reaches_the_exit()
    {
        var v=NewVillage();var store=v.Storages.First(s=>s.Goods.Any(g=>g.Kind==ResourceKind.Food));
        var (raid,b)=Battle(v,EnemyKind.Wolf);b.Units.RemoveAt(1);
        var u=b.Units[0];u.PlunderPlot=store.PlotId;u.State="Retreating";u.X=u.SpawnX;u.Y=u.SpawnY;
        var stock=v.Stock(ResourceKind.Food);
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(1));
        Assert.Equal(stock-3,v.Stock(ResourceKind.Food));Assert.Equal(3,raid.Stolen.Single().Amount);
        Assert.False(RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(2)));
        Assert.Equal(stock-3,v.Stock(ResourceKind.Food));
    }
    [Fact]
    public void Killed_carrier_does_not_duplicate_or_remove_storage_goods_and_rewards_only_once()
    {
        var v=NewVillage();var c=Guard(v,30);var (raid,b)=Battle(v,EnemyKind.Skeleton);
        b.Units.RemoveAt(1);var u=b.Units[0];u.PlunderPlot=v.Storages[0].PlotId;u.State="Retreating";
        b.Fighters.Add(new(){CitizenId=c.Id,X=15.5,Y=13.5,Health=44,MaximumHealth=44,Guard=true,Active=true,NextAttackAt=Now});
        var food=v.Stock(ResourceKind.Food);
        RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(1));
        Assert.Equal("Finished",b.Phase);Assert.Equal(1,raid.Killed);Assert.Empty(raid.Stolen);
        Assert.Equal(food,v.Stock(ResourceKind.Food));var renown=c.Renown;var iron=v.Stock(ResourceKind.IronOre);
        RaidSimulation.Tick(v,raid,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(2));
        Assert.Equal(renown,c.Renown);Assert.Equal(iron,v.Stock(ResourceKind.IronOre));
    }
    [Fact]
    public void Recovered_energy_recalculates_the_next_cycle_without_a_reload_or_second_charge()
    {
        var v=NewVillage();var c=NewCitizen(v);var context=new WorkContext{Village=v};
        c.StoredEnergy=0;c.EnergyUpdatedAt=Now;
        CycleEngine.Start(c,Task("forage"),context,Now);Assert.True(c.CycleTired);
        var tiredLength=c.CycleEndsAt!.Value-Now;
        CycleEngine.Settle(c,context,c.CycleEndsAt.Value);
        var next=c.CycleEndsAt.Value.AddHours(2);var balance=c.CurrentEnergy(next,v);
        var cost=WorkEnergy.WithMedical(c,WorkEnergy.TaskCost(v,Task("forage"),next),next);
        Assert.True(balance>=cost);
        CycleEngine.Start(c,Task("forage"),context,next);
        Assert.False(c.CycleTired);Assert.True(c.CycleEndsAt-next<tiredLength);
        Assert.Equal(balance-cost,c.StoredEnergy,8);
    }
    [Theory]
    [InlineData("{\"option\":\"none\",\"reason\":\"Я вижу")]
    [InlineData("```json\n{}\n```")]
    [InlineData("option: none, reason: Я вижу")]
    [InlineData("Я вижу {\"option\":\"none\"}")]
    public void Service_model_output_is_rejected_before_publication(string raw)
    {
        Assert.False(ChaplainSpeech.IsPlainText(raw));
        Assert.Throws<ArgumentException>(()=>ChaplainMessage.Speak(Guid.NewGuid(),raw,Now));
        Assert.True(ChaplainSpeech.IsPlainText("Соседи, кому помочь на стройке?"));
    }
}
