using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;
namespace Desertod.Domain.Tests;
public sealed class FreeCombatTests
{
    private static (Village V,Citizen C,Raid R,RaidBattle B) Scene()
    {
        var v=NewVillage();v.Id=Guid.Parse("00000000-0000-0000-0000-000000000701");
        var c=NewCitizen(v);c.CycleSettled=true;c.Spot=new(12.5,13.5);
        var r=new Raid{Enemy=EnemyKind.Zombie,StrikesAt=Now,Count=1,EngineVersion=2};v.Raids.Add(r);
        var b=new RaidBattle{Id=r.Id,VillageId=v.Id,Sector="west",Phase="Fighting",StartsAt=Now,LastTickAt=Now,EndsAt=Now.AddMinutes(4),
            Units=[new(){X=8.5,Y=13.5,Health=1000,MaximumHealth=1000,Attack=0,Speed=0,SpawnX=-5.5,SpawnY=13.5,NextAttackAt=Now.AddHours(1)}],
            Fighters=[new(){CitizenId=c.Id,X=12.5,Y=13.5,Active=true,Guard=false,Health=44,MaximumHealth=44,NextAttackAt=Now}]};
        return(v,c,r,b);
    }
    [Fact] public void No_duty_or_target_means_no_automatic_chase_or_return_to_camp()
    {
        var(v,c,r,b)=Scene();var start=b.Fighters[0].Position;
        for(var n=1;n<=5;n++)RaidSimulation.Tick(v,r,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(n));
        Assert.Equal(start,b.Fighters[0].Position);Assert.Equal(1000,b.Units[0].Health);
        b.Fighters[0].TargetUnitId=b.Units[0].Id;
        for(var n=6;n<=15;n++)RaidSimulation.Tick(v,r,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(n));
        Assert.True(b.Fighters[0].Position.DistanceTo(start)>1);Assert.True(b.Units[0].Health<1000);
        Assert.Null(c.CurrentTaskId);Assert.True(c.CycleSettled);
    }
    [Fact] public void Moving_to_chosen_ground_stops_there_without_a_sector_or_post()
    {
        var(v,c,r,b)=Scene();var f=b.Fighters[0];f.DestinationX=15.5;f.DestinationY=13.5;
        for(var n=1;n<=10;n++)RaidSimulation.Tick(v,r,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(n));
        Assert.Equal(new TilePoint(15.5,13.5),f.Position);Assert.Null(f.DestinationX);Assert.Equal("hold",f.Mode);
        RaidSimulation.Cancel(v,r,b,[c],Now.AddSeconds(11));Assert.Equal(f.Position,c.Spot);
    }
    [Fact] public void Nearby_enemy_causes_local_self_defense_without_requiring_a_guard_task()
    {
        var(v,c,r,b)=Scene();b.Fighters.Clear();b.Units[0].X=11.5;
        RaidSimulation.Tick(v,r,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(1));
        RaidSimulation.Tick(v,r,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(2));
        var f=Assert.Single(b.Fighters);Assert.False(f.Guard);Assert.Equal(c.Spot,f.Position);Assert.True(b.Units[0].Health<1000);
    }
    [Fact] public void Slow_units_advance_instead_of_bouncing_back_to_the_previous_cell_centre()
    {
        var(v,_,_,b)=Scene();var nav=new RaidNavigation(v);var unit=b.Units[0];unit.X=10.5;unit.Speed=.3;
        var previous=unit.X;
        for(var i=0;i<8;i++){Assert.True(nav.Move(unit,new(14.5,13.5),1,false,false,out _));Assert.True(unit.X>previous);previous=unit.X;}
        Assert.InRange(unit.X,12.89,12.91);Assert.True(nav.PathSearches>0);Assert.True(nav.PathNodes>0);
    }
    [Fact] public void Diagnostics_observe_only_real_steps_and_do_not_change_outcomes()
    {
        var(v,c,r,b)=Scene();RaidTickMetrics? sample=null;
        Assert.True(RaidSimulation.Tick(v,r,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(1),m=>sample=m));
        Assert.NotNull(sample);Assert.Equal(1,sample.Steps);Assert.Equal(1000,sample.BacklogMs);
        Assert.False(RaidSimulation.Tick(v,r,b,[c],new HashSet<Guid>{c.Id},Now.AddSeconds(1),_=>throw new Exception("duplicate tick")));
    }
}
