using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Stats;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class RaidEditorTests
{
    [Fact]
    public void Explicit_auto_boss_is_never_silently_replaced_by_another_variant()
    {
        var settings=RaidSettings.ForPreset("boss");
        Assert.Throws<GameRuleException>(()=>RaidSettingsPlanner.Preview(settings,8,true));
        settings.Mode="manual";settings.Count=2;settings.HealthMultiplier=3;
        var preview=RaidSettingsPlanner.Preview(settings,8,true);
        Assert.Equal(2,preview.Count);Assert.All(preview.Units,u=>Assert.Equal("boss",u.Variant));
        Assert.True(preview.ActualCost>preview.RecommendedBudget);Assert.Equal("dangerous",preview.Risk);
        Assert.Throws<GameRuleException>(()=>RaidSettingsPlanner.Preview(settings,8,false));
    }

    [Fact]
    public void Mixed_species_and_quality_are_snapshotted_and_budgeted_per_individual()
    {
        var v=NewVillage();var settings=RaidSettings.ForPreset("mixed");settings.Mode="manual";settings.Count=16;
        settings.Species=[new(){Enemy=EnemyKind.Wolf,Percent=50},new(){Enemy=EnemyKind.Skeleton,Percent=50}];
        var preview=RaidSettingsPlanner.Preview(settings,100,true);
        var raid=new Raid{Enemy=settings.Enemy,StrikesAt=Now.AddMinutes(3)};
        var battle=RaidBalance.Prepare(v,raid,[],[],new HashSet<Guid>(),Now,settings:settings);
        Assert.Contains(battle.Units,u=>u.Enemy==EnemyKind.Wolf);Assert.Contains(battle.Units,u=>u.Enemy==EnemyKind.Skeleton);
        Assert.Equal(preview.Units.Select(u=>(u.Enemy,u.Variant,u.Health,u.Attack)),battle.Units.Select(u=>(u.Enemy!.Value,u.Variant,u.Health,u.Attack)));
        settings.HealthMultiplier=12;settings.Species[0].Percent=1;
        Assert.Equal(1,battle.Settings!.HealthMultiplier);Assert.Equal(50,battle.Settings.Species[0].Percent);
        Assert.Equal(preview.ActualCost,battle.ThreatBudget,5);
    }

    [Theory]
    [InlineData("count")][InlineData("species")][InlineData("nan")][InlineData("dungeon")][InlineData("side")]
    public void Invalid_or_unsupported_compositions_are_rejected(string defect)
    {
        var s=RaidSettings.ForPreset("mixed");
        switch(defect)
        {
            case "count":s.Count=49;break;
            case "species":s.Species=[new(){Enemy=EnemyKind.Wolf,Percent=30}];break;
            case "nan":s.SpeedMultiplier=double.NaN;break;
            case "dungeon":s.Enemy=EnemyKind.BarrowKing;break;
            case "side":s.Sector="east";break;
        }
        Assert.Throws<GameRuleException>(()=>s.Validate(true));
    }

    private static (Raid Raid,RaidBattle Battle) EasyBattle()
    {
        var r=new Raid{Killed=10,Outcome=RaidOutcome.Repelled};
        return(r,new(){Id=r.Id,Sector="west",Settings=RaidSettings.ForPreset("mixed"),
            FirstContactAt=Now,LastContactAt=Now.AddSeconds(30),
            Units=Enumerable.Range(0,10).Select(_=>new RaidUnit{Health=0,MaximumHealth=20}).ToList()});
    }

    [Fact]
    public void Two_easy_fights_raise_the_next_budget_once_and_bounds_hold()
    {
        var v=NewVillage();
        for(var i=0;i<30;i++)
        {
            var(r,b)=EasyBattle();RaidAdaptationRules.Record(v,r,b,100,Now);
            var after=v.RaidAdaptation.Coefficient;
            RaidAdaptationRules.Record(v,r,b,100,Now);Assert.Equal(after,v.RaidAdaptation.Coefficient);
            if(i==0)Assert.Equal(1,after);if(i==1)Assert.Equal(1.1,after,5);
        }
        Assert.Equal(1.5,v.RaidAdaptation.Coefficient);Assert.Equal(10,v.RaidAdaptation.Recent.Count);
        var(old,oldBattle)=EasyBattle();old.Outcome=RaidOutcome.Lost;
        for(var i=0;i<15;i++)
        {
            oldBattle.Id=Guid.NewGuid();oldBattle.Events.Clear();RaidAdaptationRules.Record(v,old,oldBattle,100,Now);
        }
        Assert.Equal(.75,v.RaidAdaptation.Coefficient);
    }

    [Fact]
    public void Manual_cancelled_and_incomplete_fights_do_not_influence_adaptation()
    {
        var v=NewVillage();var(r,b)=EasyBattle();b.ManualOverride=true;RaidAdaptationRules.Record(v,r,b,100,Now);
        b.ManualOverride=false;r.Outcome=RaidOutcome.Cancelled;RaidAdaptationRules.Record(v,r,b,100,Now);
        r.Outcome=RaidOutcome.Repelled;b.FirstContactAt=null;RaidAdaptationRules.Record(v,r,b,100,Now);
        Assert.Empty(v.RaidAdaptation.Recent);Assert.Equal(1,v.RaidAdaptation.Coefficient);
    }

    [Fact]
    public void Property_damage_alone_is_not_a_heavy_defeat_and_does_not_lower_difficulty()
    {
        var v=NewVillage();var(r,b)=EasyBattle();r.Outcome=RaidOutcome.Pending;v.Raids.Add(r);
        b.VillageId=v.Id;b.StartsAt=Now;b.LastTickAt=Now;b.EndsAt=Now.AddSeconds(200);b.DamageDone=106;
        b.Damage=[new(){PlotId="wall:test",Amount=185}];
        RaidSimulation.Tick(v,r,b,[],new HashSet<Guid>(),Now.AddSeconds(1));
        Assert.Equal(RaidOutcome.PropertyDamaged,r.Outcome);Assert.False(r.HeavyDefeat);
        Assert.Equal(1,v.RaidAdaptation.Coefficient);Assert.Equal("comparable",Assert.Single(v.RaidAdaptation.Recent).Result);
    }

    [Fact]
    public void Readiness_accounts_for_armor_health_pool_energy_and_development_without_a_fixed_240_ceiling()
    {
        var v=NewVillage();var c=NewCitizen(v,(Stat.Strength,10),(Stat.Endurance,3));var online=new HashSet<Guid>{c.Id};
        var baseline=RaidBalance.Assess(v,[c],online,Now).Budget;
        c.Armor=ResourceKind.Shield;
        Assert.True(RaidBalance.Assess(v,[c],online,Now).Budget>baseline);
        var strong=Enumerable.Range(0,40).Select(_=>NewCitizen(v,(Stat.Strength,25),(Stat.Endurance,15))).ToArray();
        Assert.True(RaidBalance.Assess(v,strong,strong.Select(x=>x.Id).ToHashSet(),Now).Budget>240);
    }

    [Theory]
    [InlineData(true)][InlineData(false)]
    public void Multiple_carriers_share_the_frozen_resource_percentage_and_dead_carriers_take_nothing(bool survivors)
    {
        var v=NewVillage();v.Storages.Clear();
        var store=Desertod.Domain.Storage.StorageBuilding.Create(Desertod.Domain.Storage.StorageKind.Granary,"5,5",Now);
        var stocked=store.Put(ResourceKind.Food,100);v.Storages.Add(store);
        var settings=RaidSettings.ForPreset("swarm");settings.Mode="manual";settings.Enemy=EnemyKind.Wolf;settings.Count=12;
        var raid=new Raid{Enemy=EnemyKind.Wolf,StrikesAt=Now};v.Raids.Add(raid);
        var battle=RaidBalance.Prepare(v,raid,[],[],new HashSet<Guid>(),Now,settings:settings);battle.Phase="Retreating";
        for(var i=0;i<battle.Units.Count;i++)
        {
            var unit=battle.Units[i];unit.Health=survivors&&i>0?100:0;unit.MaximumHealth=100;
            unit.X=unit.SpawnX;unit.Y=unit.SpawnY;unit.PlunderPlot=store.PlotId;
        }
        RaidSimulation.Tick(v,raid,battle,[],new HashSet<Guid>(),Now.AddSeconds(1));
        var expected=survivors?(int)Math.Floor(stocked*.02):0;
        Assert.Equal(expected,raid.Stolen.Sum(g=>g.Amount));Assert.Equal(stocked-expected,store.Amount(ResourceKind.Food));
        RaidSimulation.Tick(v,raid,battle,[],new HashSet<Guid>(),Now.AddSeconds(2));
        Assert.Equal(stocked-expected,store.Amount(ResourceKind.Food));
    }

    [Fact]
    public void Once_the_crowd_is_full_extra_automatic_budget_increases_quality_within_the_same_budget()
    {
        var settings=RaidSettings.ForPreset("swarm");
        var preview=RaidSettingsPlanner.Preview(settings,400,true);
        Assert.Equal(48,preview.Count);Assert.True(preview.ActualCost<=400);
        Assert.All(preview.Units,u=>Assert.True(u.Health>(12+EnemyCatalog.Get(settings.Enemy).Power*2)*RaidBalance.Factor("weak")));
    }

    [Fact]
    public void Recent_presence_without_actual_combat_does_not_count_as_full_defense()
    {
        var v=NewVillage();var active=NewCitizen(v);var idle=NewCitizen(v);var(r,b)=EasyBattle();
        b.Fighters.Add(new(){CitizenId=active.Id,DamageDealt=20});
        b.Event("readiness",$"{active.Id:N},{idle.Id:N}",Now);
        RaidAdaptationRules.Record(v,r,b,100,Now);
        Assert.Equal(0,v.RaidAdaptation.Participation.Single(p=>p.CitizenId==idle.Id).Encounters);
        Assert.True(RaidBalance.Assess(v,[active],new HashSet<Guid>{active.Id},Now).Power
            >RaidBalance.Assess(v,[idle],new HashSet<Guid>{idle.Id},Now).Power);
    }
}
