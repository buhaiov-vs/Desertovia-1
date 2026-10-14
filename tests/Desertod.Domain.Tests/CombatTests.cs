using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Weather;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class CombatTests
{
    /// <summary>Returns the given rolls in order, then 0.5 for ever.</summary>
    private sealed class ScriptedRandom(params double[] rolls) : Random
    {
        private readonly Queue<double> queue = new(rolls);

        public override double NextDouble() => queue.TryDequeue(out var roll) ? roll : 0.5;

        public override int Next(int maxValue) => (int)(NextDouble() * maxValue);
    }

    /// <summary>A village old enough for beasts to notice it.</summary>
    private static Village OldVillage()
    {
        var village = NewVillage();
        village.CreatedAt = Now - CombatRules.PeacefulTime - TimeSpan.FromHours(1);
        return village;
    }

    private static WorkContext Context(Village village, Random random) => new() { Village = village, Random = random };

    [Fact]
    public void Ambush_SparesNewVillages()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var context = Context(village, new ScriptedRandom(0, 0, 0, 0));

        CycleEngine.Start(citizen, Task("hunt"), context, Now);
        var report = Finish(citizen, context);

        Assert.Null(report.Fight);
        Assert.NotEmpty(report.Produced);
    }

    [Fact]
    public void Ambush_Lost_BringsNothingHome_AndWoundsTheHunter()
    {
        var village = OldVillage();
        var citizen = NewCitizen(village);
        // Attacked, by the wolf, the fight lost, the wound not stopped.
        var context = Context(village, new ScriptedRandom(0, 0, 0.99, 0));

        CycleEngine.Start(citizen, Task("hunt"), context, Now);
        var endsAt = citizen.CycleEndsAt!.Value;
        var report = Finish(citizen, context);

        Assert.NotNull(report.Fight);
        Assert.Equal(EnemyKind.Wolf, report.Fight.Enemy);
        Assert.False(report.Fight.Won);
        Assert.True(report.Fight.Injured);
        Assert.Empty(report.Produced);
        Assert.True(report.Overflowed);
        Assert.Equal(endsAt + TimeSpan.FromMinutes(EnemyCatalog.Get(EnemyKind.Wolf).InjuryMinutes), citizen.InjuredUntil);

        var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("forage"), context, endsAt));
        Assert.Equal("citizen.injured", error.Code);
    }

    [Fact]
    public void Ambush_Won_LeavesLoot_AndTrainsGuarding()
    {
        var village = OldVillage();
        var citizen = NewCitizen(village);
        var leather = village.Stock(ResourceKind.Leather);
        // Attacked, by the wolf, the fight won, no wound.
        var context = Context(village, new ScriptedRandom(0, 0, 0, 0.99));

        CycleEngine.Start(citizen, Task("hunt"), context, Now);
        var report = Finish(citizen, context);

        Assert.True(report.Fight!.Won);
        Assert.False(report.Fight.Injured);
        Assert.Null(citizen.InjuredUntil);
        Assert.True(village.Stock(ResourceKind.Leather) > leather);
        Assert.Contains(report.Produced, p => p.Resource == ResourceKind.Food);
        Assert.True(citizen.SkillLevel(Skill.Guarding) > 0 || citizen.Skills.Any(s => s.Kind == Skill.Guarding && s.Experience > 0));
    }

    [Fact]
    public void Armor_StopsTheWound()
    {
        var village = OldVillage();
        village.Store(ResourceKind.Shield, 1);
        var citizen = NewCitizen(village);
        // Attacked, by the wolf, the fight lost, the blow lands just past the shield's protection.
        var context = Context(village, new ScriptedRandom(0, 0, 0.99, 0.61));

        CycleEngine.Start(citizen, Task("hunt"), context, Now);
        var report = Finish(citizen, context);

        Assert.False(report.Fight!.Won);
        Assert.False(report.Fight.Injured);
        Assert.Equal(ResourceKind.Shield, report.Fight.Armor);
        Assert.Null(citizen.InjuredUntil);
    }

    [Fact]
    public void Heal_UsesARemedy()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        CombatRules.Injure(citizen, TimeSpan.FromMinutes(30), Now);

        Assert.Equal("heal.none", Assert.Throws<GameRuleException>(() => CombatRules.Heal(citizen, village, Now)).Code);

        village.Store(ResourceKind.Remedy, 1);
        CombatRules.Heal(citizen, village, Now);

        Assert.Null(citizen.InjuredUntil);
        Assert.Equal(0, village.Stock(ResourceKind.Remedy));
        Assert.Equal("heal.notInjured", Assert.Throws<GameRuleException>(() => CombatRules.Heal(citizen, village, Now)).Code);
    }

    [Fact]
    public void Arm_TakesTheBestWeaponAndArmor_AndPutsThemBack()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Spear, 1);
        village.Store(ResourceKind.Sword, 1);
        village.Store(ResourceKind.LeatherArmor, 1);
        var citizen = NewCitizen(village);
        var context = WorkContext.For(village);

        CycleEngine.Start(citizen, Task("hunt"), context, Now);

        Assert.Equal(ResourceKind.Sword, citizen.Weapon);
        Assert.Equal(ResourceKind.LeatherArmor, citizen.Armor);
        Assert.Equal(0, village.Stock(ResourceKind.Sword));
        Assert.Equal(1, village.Stock(ResourceKind.Spear));
        Assert.Equal(CombatRules.Power(NewCitizen(village)) + 6, CombatRules.Power(citizen), 6);

        Finish(citizen, context);

        Assert.Null(citizen.Weapon);
        Assert.Null(citizen.Armor);
        Assert.Equal(1, village.Stock(ResourceKind.Sword));
        Assert.Equal(1, village.Stock(ResourceKind.LeatherArmor));
    }

    [Fact]
    public void Arm_SkipsSafeWork()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Spear, 1);
        var citizen = NewCitizen(village);

        CycleEngine.Start(citizen, Task("forage"), WorkContext.For(village), Now);

        Assert.Null(citizen.Weapon);
        Assert.Equal(1, village.Stock(ResourceKind.Spear));
    }

    [Fact]
    public void Bow_NeedsArrows()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Bow, 1);
        village.Store(ResourceKind.Spear, 1);
        var citizen = NewCitizen(village);

        CombatRules.Arm(citizen, village);
        Assert.Equal(ResourceKind.Spear, citizen.Weapon);
        CombatRules.Disarm(citizen, village);

        village.Store(ResourceKind.Arrows, 10);
        CombatRules.Arm(citizen, village);
        Assert.Equal(ResourceKind.Bow, citizen.Weapon);
        Assert.Equal(ArmoryCatalog.QuiverSize, citizen.Arrows);
        Assert.Equal(10 - ArmoryCatalog.QuiverSize, village.Stock(ResourceKind.Arrows));
    }

    [Fact]
    public void Cancel_PutsTheWeaponsBack()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Spear, 1);
        var citizen = NewCitizen(village);
        CycleEngine.Start(citizen, Task(GameCatalog.KeepWatch), WorkContext.For(village), Now);
        Assert.Equal(ResourceKind.Spear, citizen.Weapon);

        CycleEngine.Cancel(citizen, Now.AddSeconds(10), village);

        Assert.Null(citizen.Weapon);
        Assert.Equal(1, village.Stock(ResourceKind.Spear));
    }

    [Fact]
    public void Raid_RepelledByWatchers_LeavesLoot()
    {
        var village = OldVillage();
        village.Store(ResourceKind.Spear, 2);
        var first = NewCitizen(village, (Stat.Strength, 5), (Stat.Dexterity, 5));
        var second = NewCitizen(village, (Stat.Strength, 5), (Stat.Dexterity, 5));
        foreach (var citizen in new[] { first, second })
        {
            CycleEngine.Start(citizen, Task(GameCatalog.KeepWatch), WorkContext.For(village), Now);
        }

        var raid = new Raid { Source = RaidSource.Night, Enemy = EnemyKind.Wolf, Count = 2, AnnouncedAt = Now, StrikesAt = Now.AddMinutes(1) };
        village.Raids.Add(raid);
        RaidRules.UpdateNextRaidAt(village);
        var leather = village.Stock(ResourceKind.Leather);

        var report = RaidRules.Resolve(village, raid, [first, second], [], new ScriptedRandom(0.5, 0.99, 0.99), Now.AddMinutes(1));

        Assert.True(report.Repelled);
        Assert.Equal(2, report.Defenders.Count);
        Assert.Equal(RaidOutcome.Repelled, raid.Outcome);
        Assert.True(report.Killed >= 1);
        Assert.Equal(leather + report.Killed, village.Stock(ResourceKind.Leather));
        Assert.True(report.MoraleChange > 0);
        Assert.True(first.Renown > 0);
        Assert.Null(village.NextRaidAt);
        Assert.Empty(RaidRules.Due(village, Now.AddHours(1)));
    }

    [Fact]
    public void Raid_Lost_StealsDamagesAndWounds()
    {
        var village = OldVillage();
        var sleeper = NewCitizen(village);
        var food = village.Stock(ResourceKind.Food);
        var raid = new Raid { Source = RaidSource.Night, Enemy = EnemyKind.Boar, Count = 3, AnnouncedAt = Now, StrikesAt = Now };
        village.Raids.Add(raid);

        var report = RaidRules.Resolve(village, raid, [sleeper], [], new ScriptedRandom(0, 0, 0, 0, 0, 0, 0), Now);

        Assert.False(report.Repelled);
        Assert.Empty(report.Defenders);
        Assert.Single(report.Victims);
        Assert.True(CombatRules.IsInjured(sleeper, Now));
        var boar = EnemyCatalog.Get(EnemyKind.Boar);
        var survivors = raid.Count - report.Killed;
        Assert.Equal(food - boar.Steals.Single(s => s.Resource == ResourceKind.Food).Amount * survivors, village.Stock(ResourceKind.Food));
        Assert.Equal(boar.Damage * survivors, village.Storages[0].Damage);
        Assert.True(report.MoraleChange < 0);
        Assert.Equal(RaidOutcome.Lost, raid.Outcome);
    }

    [Fact]
    public void Raid_OnlyCitizensOnWatchAtTheStrike_Defend()
    {
        var village = NewVillage();
        var watcher = NewCitizen(village);
        var forager = NewCitizen(village);
        CycleEngine.Start(watcher, Task(GameCatalog.KeepWatch), WorkContext.For(village), Now);
        CycleEngine.Start(forager, Task("forage"), WorkContext.For(village), Now);

        Assert.NotNull(RaidRules.GuardTask(watcher, Now.AddMinutes(5)));
        Assert.Null(RaidRules.GuardTask(watcher, watcher.CycleEndsAt!.Value));
        Assert.Null(RaidRules.GuardTask(forager, Now.AddSeconds(30)));
    }

    [Fact]
    public void Defense_GrowsWithThePalisadeAndACaptain()
    {
        var village = NewVillage();
        var bare = RaidRules.Defense(village, [], []);
        var walled = RaidRules.Defense(village, [], [Projects.ProjectCatalog.Palisade]);
        var captain = NewCitizen(village, (Stat.Endurance, 10));
        var commanded = RaidRules.Defense(village, [(captain, Task("command"))], []);

        Assert.Equal(RaidRules.CampfireDefense, bare, 6);
        Assert.True(walled > bare);
        Assert.Equal((CombatRules.Power(captain) + RaidRules.CampfireDefense) * (1 + RaidRules.CommandPerEndurance * 10), commanded, 6);
    }

    [Fact]
    public void PlanNight_DecidesOncePerNight_AndWarnsAhead()
    {
        var village = OldVillage();
        var period = DayCycle.PeriodOf(Now);
        var night = DayCycle.IsNight(period) ? period : period + 1;
        var dusk = DayCycle.PeriodStart(night);

        Assert.Null(RaidRules.PlanNight(village, 4, new ScriptedRandom(0), DayCycle.PeriodStart(night - 1)));
        var raid = RaidRules.PlanNight(village, 4, new ScriptedRandom(0, 0, 0.5), dusk);

        Assert.NotNull(raid);
        Assert.Equal(EnemyKind.Wolf, raid.Enemy);
        Assert.InRange(raid.StrikesAt, dusk + DayCycle.PeriodLength * RaidRules.EarliestStrike, dusk + DayCycle.PeriodLength * RaidRules.LatestStrike);
        Assert.Equal(raid.StrikesAt - RaidRules.Warning, raid.AnnouncedAt);
        Assert.Equal(raid.StrikesAt, village.NextRaidAt);
        Assert.Null(RaidRules.PlanNight(village, 4, new ScriptedRandom(0), dusk.AddMinutes(1)));
    }

    [Fact]
    public void PlanNight_SparesNewAndEmptyVillages()
    {
        var period = DayCycle.PeriodOf(Now);
        var night = DayCycle.IsNight(period) ? period : period + 1;
        var dusk = DayCycle.PeriodStart(night);
        var young = NewVillage();
        young.CreatedAt = dusk;

        Assert.Null(RaidRules.PlanNight(young, 4, new ScriptedRandom(0), dusk));
        Assert.Null(RaidRules.PlanNight(OldVillage(), 0, new ScriptedRandom(0), dusk));
    }

    [Fact]
    public void CallUndead_SendsWaves_OneEventAtATime()
    {
        var village = NewVillage();

        var waves = RaidRules.CallUndead(village, EnemyKind.Zombie, RaidSize.Large, 2, Now);

        Assert.Equal(3, waves.Count);
        Assert.All(waves, w => Assert.Equal(Now, w.AnnouncedAt));
        Assert.Equal(Now + RaidRules.UndeadWarning, village.NextRaidAt);
        Assert.Equal(waves[0].StrikesAt + RaidRules.WaveInterval, waves[1].StrikesAt);
        Assert.Equal("raid.undeadComing",
            Assert.Throws<GameRuleException>(() => RaidRules.CallUndead(village, EnemyKind.Skeleton, RaidSize.Small, 2, Now)).Code);
        Assert.Equal("raid.notUndead",
            Assert.Throws<GameRuleException>(() => RaidRules.CallUndead(NewVillage(), EnemyKind.Bear, RaidSize.Small, 2, Now)).Code);

        RaidRules.CancelUndead(village);
        Assert.Empty(village.Raids);
        Assert.Null(village.NextRaidAt);
    }

    [Fact]
    public void Repair_MendsTheMostDamagedBuilding()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Wood, 10);
        var citizen = NewCitizen(village);
        var context = WorkContext.For(village);

        Assert.Equal("repair.nothing",
            Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("repair"), context, Now)).Code);

        village.Storages[1].Damage = 60;
        CycleEngine.Start(citizen, Task("repair"), context, Now);
        var report = Finish(citizen, context);

        Assert.True(report.Repaired > 0);
        Assert.Equal(60 - report.Repaired, village.Storages[1].Damage);
    }

    [Fact]
    public void DamagedBuildings_WorkSlower()
    {
        var village = NewVillage();
        village.Structures.Add(new Structure { Kind = BuildingKind.Smithy, PlotId = "1,1", BuiltAt = Now, Damage = CombatRules.MaximumDamage });

        Assert.Equal(0.5, CombatRules.BuildingMultiplier(village, Task("smelt-ingot")), 6);
        Assert.Equal(1, CombatRules.BuildingMultiplier(village, Task("forage")), 6);
    }

    [Fact]
    public void Weapons_AreCraftedOnceDiscovered()
    {
        Assert.Equal(Technologies.TechnologyCatalog.Weaponcraft, Task("make-spears").RequiredTechnology);
        Assert.Equal(Technologies.TechnologyCatalog.Archery, Task("make-arrows").RequiredTechnology);
        Assert.Equal(Technologies.TechnologyCatalog.Warfare, Task("forge-sword").RequiredTechnology);
        Assert.Equal(Technologies.TechnologyCatalog.Warfare, GameCatalog.FindRole("guard")!.Technology);
        Assert.NotEmpty(BuildingCatalog.Get(BuildingKind.Barracks).Stages);
    }
}
