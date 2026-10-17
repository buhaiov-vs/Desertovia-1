using System.Diagnostics;
using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Director;
using Desertod.Domain.Entities;
using Desertod.Domain.Stats;
using Desertod.Domain.World;
using Xunit.Abstractions;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class MonsterBehaviorTests(ITestOutputHelper output)
{
    [Fact]
    public void Local_avoidance_cannot_cut_a_blocked_diagonal_corner()
    {
        Assert.False(MonsterCrowd.CanTraverse(new(.9,.9),new(1.1,1.1),(x,y)=>(x,y) is not (1,0) and not (0,1)));
        Assert.True(MonsterCrowd.CanTraverse(new(.9,.9),new(1.1,1.1),(_,_)=>true));
        Assert.False(MonsterCrowd.CanTraverse(new(.5,.5),new(2.5,.5),(x,_)=>x!=1));
    }
    [Fact]
    public void All_monster_kinds_have_a_default_without_enabling_dungeon_raids()
    {
        foreach(var enemy in EnemyCatalog.Enemies)
        {
            Assert.False(string.IsNullOrWhiteSpace(MonsterBrain.Profile(enemy,"regular").Role));
            Assert.NotNull(MonsterBrain.Choose(MonsterBrain.Profile(enemy,"regular"),new(0,0),null,
                [new("building","a",new(2,0),6)]));
        }
        Assert.DoesNotContain(AlmightyPolicy.RaidOptionIds(),id=>id.StartsWith("barrowking"));
    }

    [Fact]
    public void Property_wins_over_an_idle_player_but_an_attacker_can_distract_the_raider()
    {
        var profile=MonsterBrain.Profile(EnemyCatalog.Get(EnemyKind.Zombie),"regular");
        var property=new MonsterObjective("building","house",new(1,0),6);
        var player=new MonsterObjective("citizen","player",new(1,0),3);
        Assert.Equal("house",MonsterBrain.Choose(profile,new(0,0),null,[player,property])!.Id);
        Assert.Equal("player",MonsterBrain.Choose(profile,new(0,0),null,[property,player with{Value=12}])!.Id);
        Assert.Equal("other",MonsterBrain.Choose(profile,new(0,0),"house",
            [property with{Occupied=8},new("building","other",new(2,0),6)])!.Id);
    }

    [Theory]
    [InlineData("swarm")][InlineData("elite")][InlineData("mixed")][InlineData("boss")]
    public void Frozen_compositions_pay_for_every_unit_in_the_same_budget(string composition)
    {
        var v=NewVillage();var citizens=Enumerable.Range(0,12).Select(_=>NewCitizen(v,(Stat.Strength,12))).ToArray();
        var online=citizens.Select(c=>c.Id).ToHashSet();
        var raid=new Raid{Enemy=EnemyKind.Skeleton,StrikesAt=Now.AddMinutes(3)};
        var b=RaidBalance.Prepare(v,raid,citizens,[],online,Now,"west",composition);
        Assert.Equal(composition,b.Composition);
        Assert.Equal(RaidBalance.Count(raid.Enemy,composition,b.ThreatBudget),b.Units.Count);
        Assert.True(b.Units.Sum(u=>EnemyCatalog.Get(raid.Enemy).Power*Math.Pow(RaidBalance.Factor(u.Variant),2))<=b.ThreatBudget+.001);
        if(composition=="boss"){Assert.InRange(b.Units.Count,1,3);Assert.All(b.Units,u=>Assert.Equal("boss",u.Variant));}
        var before=b.Units.Select(u=>(u.Id,u.Health,u.X,u.Y)).ToArray();
        Assert.False(RaidSimulation.Tick(v,raid,b,citizens,online,Now.AddSeconds(1)));
        Assert.Equal(before,b.Units.Select(u=>(u.Id,u.Health,u.X,u.Y)).ToArray());
    }

    [Fact]
    public void An_unaffordable_boss_is_not_offered_or_created_with_free_power()
    {
        Assert.Equal("elite",RaidBalance.NormalizeComposition(EnemyKind.Skeleton,"boss",8));
        var facts=new AlmightyFacts(10,3,3,0,0,0,0,0,false,false);
        // Проверка полного набора не ослабляет блокировки директора.
        var options=AlmightyPolicy.Options(facts,[new("west",[])],8);
        Assert.DoesNotContain(options,o=>o.Composition=="boss");
        Assert.InRange(AlmightyPolicy.RaidOptionIds().Length,1,64);
    }

    private static (Village V,Raid Raid,RaidBattle Battle,RaidUnit Boss) AtProperty()
    {
        var v=NewVillage();v.Id=Guid.Parse("00000000-0000-0000-0000-000000000701");v.Wilds=Wilderness.Generate(v.Id);v.Storages.Clear();
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="5,10"});
        var p=WorldMap.Yard("5,10");
        var boss=new RaidUnit{Variant="boss",X=p.X,Y=p.Y,SpawnX=-6.5,SpawnY=13.5,Health=200,MaximumHealth=200,Attack=10,Speed=0};
        var raid=new Raid{Enemy=EnemyKind.Zombie,StrikesAt=Now,EngineVersion=2};v.Raids.Add(raid);
        var b=new RaidBattle{Id=raid.Id,VillageId=v.Id,Sector="west",Composition="boss",Phase="Raiding",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(4),Units=[boss]};
        return(v,raid,b,boss);
    }

    [Fact]
    public void Boss_warns_before_property_damage_and_repeated_ticks_cannot_repeat_the_blow()
    {
        var(v,raid,b,boss)=AtProperty();
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(1));
        Assert.Equal(0,v.Structures.Single().Damage);Assert.Equal(Now.AddSeconds(4),boss.StrikeAt);
        Assert.Equal(WorldMap.Yard("5,10").X,boss.StrikeX);Assert.Equal("Windup",boss.State);
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(4));
        var damage=v.Structures.Single().Damage;Assert.True(damage>0);Assert.Null(boss.StrikeAt);
        Assert.False(RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(4)));
        Assert.Equal(damage,v.Structures.Single().Damage);
        boss.Health=90;
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(5));
        Assert.Equal(2,boss.BossPhase);Assert.Equal(90,boss.Health);
    }

    [Fact]
    public void Cancelling_a_prepared_boss_strike_preserves_property()
    {
        var(v,raid,b,boss)=AtProperty();
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(1));
        RaidSimulation.Cancel(v,raid,b,[],Now.AddSeconds(2));
        Assert.False(RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(5)));
        Assert.Equal(0,v.Structures.Single().Damage);Assert.False(boss.Alive);
    }

    [Fact]
    public void An_outage_freezes_the_warning_instead_of_delivering_an_unseen_boss_strike()
    {
        var(v,raid,b,boss)=AtProperty();
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(1));
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddHours(1));
        Assert.Equal(0,v.Structures.Single().Damage);Assert.True(boss.StrikeAt>Now.AddHours(1));
        Assert.Equal(200,boss.Health);
    }

    [Fact]
    public void Raiders_distribute_between_property_targets_instead_of_chasing_distant_players()
    {
        var(v,raid,b,_)=AtProperty();v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="13,10"});b.Units.Clear();
        for(var i=0;i<12;i++)b.Units.Add(new(){X=9.5+i*.02,Y=14.5,Health=100,MaximumHealth=100,Speed=0,Attack=1});
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(1));
        Assert.InRange(b.Units.Count(u=>u.TargetKind=="building"),6,10);
        Assert.Equal(2,b.Units.Where(u=>u.TargetKind=="building").Select(u=>u.TargetId).Distinct().Count());
        Assert.DoesNotContain(b.Units,u=>u.State=="Retreating");
    }

    [Fact]
    public void Only_an_escaping_live_carrier_takes_real_goods_and_the_raid_has_a_total_limit()
    {
        var(v,raid,b,_)=AtProperty();raid.Enemy=EnemyKind.Wolf;b.Units.Clear();b.Phase="Retreating";
        var store=Desertod.Domain.Storage.StorageBuilding.Create(Desertod.Domain.Storage.StorageKind.Granary,"5,5",Now);
        store.OwnerAccountId=Guid.NewGuid();var stocked=store.Put(Desertod.Domain.Catalog.ResourceKind.Food,100);v.Storages.Add(store);
        for(var i=0;i<12;i++)b.Units.Add(new(){Health=i==0?0:100,MaximumHealth=100,X=10.5,Y=14.5,SpawnX=10.5,SpawnY=14.5,PlunderPlot=store.PlotId});
        RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(1));
        Assert.Equal(24,raid.Stolen.Sum(g=>g.Amount));Assert.Equal(stocked-24,store.Amount(Desertod.Domain.Catalog.ResourceKind.Food));
        Assert.False(RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(2)));
        Assert.Equal(stocked-24,store.Amount(Desertod.Domain.Catalog.ResourceKind.Food));
    }

    [Theory]
    [InlineData(16)][InlineData(32)][InlineData(48)]
    public void Full_village_navigation_stays_bounded_in_a_long_crowded_raid(int count)
    {
        var v=NewVillage();v.Id=Guid.Parse("00000000-0000-0000-0000-000000000701");v.Wilds=Wilderness.Generate(v.Id);
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="5,10"});
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="21,10"});
        var citizens=Enumerable.Range(0,24).Select(_=>NewCitizen(v,(Stat.Strength,20))).ToArray();
        var raid=new Raid{Enemy=EnemyKind.Skeleton,StrikesAt=Now};v.Raids.Add(raid);
        var b=RaidBalance.Prepare(v,raid,citizens,[Desertod.Domain.Projects.ProjectCatalog.Palisade],citizens.Select(c=>c.Id).ToHashSet(),Now,"west","swarm");
        Assert.Equal(48,b.Units.Count);b.Units.RemoveRange(count,b.Units.Count-count);
        foreach(var gate in v.Fortifications.Where(f=>f.Gate))gate.Open=false;
        var timings=new List<double>();var searches=0;
        for(var second=1;second<=120&&b.FinishedAt is null;second++)
        {
            RaidSimulation.Tick(v,raid,b,[],new HashSet<Guid>(),Now.AddSeconds(second),metrics=>{
                timings.Add(metrics.ElapsedMs);searches=Math.Max(searches,metrics.PathSearches);
                Assert.InRange(metrics.PathNodes,0,(count+1)*5100);
            });
            Assert.InRange(b.DamageDone,0,RaidSimulation.PlunderDamageLimit);
            Assert.InRange(b.Damage.Where(d=>d.PlotId.StartsWith("wall:")).Sum(d=>d.Amount),0,RaidSimulation.WallDamageLimit);
        }
        Assert.InRange(searches,1,count);
        var nav=new RaidNavigation(v);
        Assert.All(b.Units,u=>Assert.True(nav.Dry((int)Math.Floor(u.X),(int)Math.Floor(u.Y))));
        timings.Sort();output.WriteLine($"Full raid units={count},steps={timings.Count},p95Ms={timings[(int)(timings.Count*.95)]:F2},maxMs={timings[^1]:F2},maxSearches={searches},damage={b.DamageDone}");
    }

    [Theory]
    [InlineData(16)][InlineData(32)][InlineData(48)]
    public void Crowd_is_bounded_deterministic_and_cannot_cross_the_world_geometry(int count)
    {
        var actors=Enumerable.Range(0,count).Select(i=>new CrowdActor(i,new(i%8*.7,i/8*.7),new(8,1.5),.7)).ToArray();
        static bool Allowed(TilePoint from,TilePoint to)=>to.X<=4&&to.Y>=-.5;
        var watch=Stopwatch.StartNew();
        var first=MonsterCrowd.Move(actors,1,Allowed);
        var second=MonsterCrowd.Move(actors,1,Allowed);
        foreach(var actor in actors)
        {
            Assert.Equal(first[actor.Id],second[actor.Id]);
            Assert.True(double.IsFinite(first[actor.Id].X));
            Assert.InRange(actor.Position.DistanceTo(first[actor.Id]),0,.701);
            if(actor.Position.X<=4)Assert.True(first[actor.Id].X<=4);
        }
        Assert.Contains(first,p=>p.Value.DistanceTo(actors[p.Key].Position)>.05);
        output.WriteLine($"ORCA actors={count}, two steps cpuMs={watch.Elapsed.TotalMilliseconds:F2}");
    }
}
