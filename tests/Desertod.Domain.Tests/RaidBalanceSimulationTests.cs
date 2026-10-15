using System.Diagnostics;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.World;
using Xunit.Abstractions;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

/// <summary>Clock-driven encounters with actual actors/navigation, not an aggregate win-probability formula.</summary>
public sealed class RaidBalanceSimulationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(2,"swarm",false)][InlineData(2,"elite",false)][InlineData(2,"mixed",true)]
    [InlineData(6,"swarm",true)][InlineData(6,"elite",false)][InlineData(6,"mixed",true)]
    public void Announced_encounters_finish_with_bounded_losses_and_persisted_individual_outcomes(int count,string profile,bool walls)
    {
        var watch=Stopwatch.StartNew();
        var v=NewVillage();v.Wilds!.Obstacles.Clear();v.Wilds.Ravine.Clear();
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="5,10"});
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="21,10"});
        var guards=Enumerable.Range(0,count).Select(i=>{
            var c=NewCitizen(v,$"Guard{i}",Now,(Stat.Strength,2+count/2),(Stat.Dexterity,2),(Stat.Endurance,3));
            c.CurrentTaskId=GameCatalog.KeepWatch;c.CycleStartedAt=Now;c.CycleEndsAt=Now.AddHours(1);c.CycleSettled=false;
            return c;
        }).ToArray();
        var ids=guards.Select(c=>c.Id).ToHashSet();
        var raid=new Raid{Id=Guid.Parse("00000001-0000-0000-0000-000000000001"),Enemy=EnemyKind.Skeleton,AnnouncedAt=Now,StrikesAt=Now.AddMinutes(3)};
        v.Raids.Add(raid);
        var b=RaidBalance.Prepare(v,raid,guards,walls?[ProjectCatalog.Palisade]:[],ids,Now,"west",profile);
        if(walls)foreach(var gate in v.Fortifications.Where(f=>f.Gate))gate.Open=false;
        var initialUnits=b.Units.Count;var budget=b.ThreatBudget;
        var terrain=AuthoredTerrain.For(v.Id,v.Wilds);
        for(var second=1;second<=330&&b.FinishedAt is null;second++)
        {
            RaidSimulation.Tick(v,raid,b,guards,ids,b.StartsAt.AddSeconds(second));
            Assert.InRange(b.DamageDone,0,RaidSimulation.PlunderDamageLimit);
            Assert.All(b.Units,u=>{
                Assert.InRange(u.Health,0,u.MaximumHealth);
                Assert.False(AuthoredTerrain.Blocked(terrain[AuthoredTerrain.Index((int)Math.Floor(u.X),(int)Math.Floor(u.Y))]));
            });
        }
        Assert.NotNull(b.FinishedAt);Assert.NotNull(raid.ResolvedAt);
        Assert.Equal(initialUnits,b.Units.Count);Assert.Equal(budget,b.ThreatBudget);
        Assert.Equal(b.Units.Count(u=>u.Health<=0),raid.Killed);
        Assert.Equal(b.Fighters.Count(f=>f.Wounded),raid.Injured);
        Assert.DoesNotContain(b.Events,e=>e.Target=="camp"&&e.Kind=="damage");
        output.WriteLine($"guards={count} profile={profile} walls={walls} units={initialUnits} budget={budget:F1} killed={raid.Killed} wounded={raid.Injured} damage={b.DamageDone} outcome={raid.Outcome} simulatedSeconds={(b.FinishedAt-b.StartsAt)?.TotalSeconds} cpuMs={watch.ElapsedMilliseconds}");
    }
}
