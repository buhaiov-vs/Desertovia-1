using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class DungeonTests
{
    /// <summary>Every roll succeeds and every amount is the smallest.</summary>
    private sealed class LuckyRandom : Random
    {
        public override double NextDouble() => 0;

        public override int Next(int minValue, int maxValue) => minValue;
    }

    private static DungeonDefinition Corridor(string middle, double timeLimitSeconds = 900) =>
        DungeonCatalog.Parse("test", "Test", ["TTTTTTTTTT", "E" + middle + "T", "TTTTTTTTTT"], TimeSpan.FromSeconds(timeLimitSeconds));

    private static DungeonFighter Fighter(HeroClass heroClass = HeroClass.Brawler, double health = 100, double damage = 1, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), "Hero", heroClass, health, damage, 0, null, false);

    private static void Run(DungeonSimulation simulation, double seconds, Random? random = null)
    {
        random ??= new Random(1);
        for (var elapsed = 0.0; elapsed < seconds - 1e-9 && !simulation.IsOver; elapsed += 0.1)
        {
            simulation.Tick(0.1, random);
        }
    }

    [Fact]
    public void DeadLands_EveryFoeCanBeReachedFromTheEntrance()
    {
        var dungeon = DungeonCatalog.Get(DungeonCatalog.DeadLands);
        var entrance = ((int)dungeon.Entrance.X, (int)dungeon.Entrance.Y);

        Assert.Equal(48, dungeon.Width);
        Assert.Equal(32, dungeon.Height);
        Assert.Contains(dungeon.Spawns, s => s.Kind == EnemyKind.BarrowKing);
        Assert.All(dungeon.Spawns, spawn => Assert.NotNull(DungeonPathfinder.Find(dungeon, entrance, (spawn.X, spawn.Y))));
    }

    [Fact]
    public void DungeonFoes_NeverAnswerTheTsarsCall()
    {
        Assert.All(DungeonCatalog.Foes.Keys.Where(k => k is not EnemyKind.Zombie and not EnemyKind.Skeleton),
            kind => Assert.True(EnemyCatalog.Get(kind).DungeonOnly));
    }

    [Fact]
    public void AvailableClasses_FollowTheGearInStorageAndTheCrafts()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);

        Assert.Equal(new[] { HeroClass.Brawler }, DungeonRules.AvailableClasses(citizen, village));

        village.Store(ResourceKind.Spear, 1);
        village.Store(ResourceKind.Sword, 1);
        village.Store(ResourceKind.Remedy, 1);
        Assert.Equal(new[] { HeroClass.Brawler, HeroClass.Spearman }, DungeonRules.AvailableClasses(citizen, village));

        citizen.WithSkill(Skill.Guarding, 2).WithSkill(Skill.Scholarship, 2);
        Assert.Equal(new[] { HeroClass.Brawler, HeroClass.Spearman, HeroClass.Swordsman, HeroClass.Healer }, DungeonRules.AvailableClasses(citizen, village));

        village.Store(ResourceKind.Bow, 1);
        citizen.WithSkill(Skill.Hunting, 2);
        Assert.DoesNotContain(HeroClass.Archer, DungeonRules.AvailableClasses(citizen, village));
        village.Store(ResourceKind.Arrows, 3);
        village.Store(ResourceKind.Shield, 1);
        Assert.Contains(HeroClass.Archer, DungeonRules.AvailableClasses(citizen, village));
        Assert.Contains(HeroClass.Shieldbearer, DungeonRules.AvailableClasses(citizen, village));
        Assert.DoesNotContain(HeroClass.Voivode, DungeonRules.AvailableClasses(citizen, village));
    }

    [Fact]
    public void Join_TakesTheGearFromStorage_AndLeaving_PutsItBack()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village).WithSkill(Skill.Guarding, 2);
        village.Store(ResourceKind.Sword, 1);
        village.Store(ResourceKind.LeatherArmor, 1);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);

        DungeonRules.Join(run, village, citizen, HeroClass.Swordsman, Now);

        Assert.Equal(ResourceKind.Sword, citizen.Weapon);
        Assert.Equal(ResourceKind.LeatherArmor, citizen.Armor);
        Assert.Equal(0, village.Stock(ResourceKind.Sword));
        Assert.Equal(run.Id, citizen.DungeonRunId);
        Assert.Equal(2, run.Heroes.Count);

        DungeonRules.Leave(run, village, citizen, Now);

        Assert.Null(citizen.Weapon);
        Assert.Null(citizen.Armor);
        Assert.Equal(1, village.Stock(ResourceKind.Sword));
        Assert.Equal(1, village.Stock(ResourceKind.LeatherArmor));
        Assert.Null(citizen.DungeonRunId);
        Assert.Single(run.Heroes);
    }

    [Fact]
    public void Join_ChangingClass_SwapsTheGear()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        village.Store(ResourceKind.Spear, 1);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);

        DungeonRules.Join(run, village, citizen, HeroClass.Spearman, Now);
        Assert.Equal(new[] { HeroClass.Brawler, HeroClass.Spearman }, DungeonRules.AvailableClasses(citizen, village, run.Hero(citizen.Id)));
        DungeonRules.Join(run, village, citizen, HeroClass.Brawler, Now);

        Assert.Null(citizen.Weapon);
        Assert.Equal(1, village.Stock(ResourceKind.Spear));
        Assert.Equal(HeroClass.Brawler, run.Hero(citizen.Id)!.Class);
    }

    [Fact]
    public void Join_CancelsTheRunningTask_AndBlocksNewOnes()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var context = new Rules.WorkContext { Village = village, Random = new Random(1) };
        Rules.CycleEngine.Start(citizen, Task("forage"), context, Now);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);

        DungeonRules.Join(run, village, citizen, HeroClass.Brawler, Now);

        Assert.Null(citizen.CurrentTaskId);
        var error = Assert.Throws<GameRuleException>(() => Rules.CycleEngine.Start(citizen, Task("forage"), context, Now));
        Assert.Equal("dungeon.busy", error.Code);
    }

    [Fact]
    public void Join_IsClosedOnceTheGatheringEnds()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);

        var error = Assert.Throws<GameRuleException>(() => DungeonRules.Join(run, village, citizen, HeroClass.Brawler, run.GatheringEndsAt));

        Assert.Equal("dungeon.notGathering", error.Code);
    }

    [Fact]
    public void Heroes_DoNotAttackByThemselves()
    {
        var simulation = new DungeonSimulation(Corridor(".r......"), [Fighter(health: 1000, damage: 50)]);

        Run(simulation, 3);

        Assert.All(simulation.Foes, foe => Assert.Equal(foe.Definition.Health, foe.Health));
        Assert.True(simulation.Heroes[0].Health < 1000);
    }

    [Fact]
    public void Foes_ChaseAHeroInsideTheirAggroRange_AndLeaveOthersAlone()
    {
        var near = new DungeonSimulation(Corridor("....r..."), [Fighter(health: 1000)]);
        var far = new DungeonSimulation(Corridor(".......r"), [Fighter(health: 1000)]);

        Run(near, 3);
        Run(far, 3);

        Assert.True(near.Foes[0].X < 5.5);
        Assert.True(near.Heroes[0].Health < 1000);
        Assert.Equal(8.5, far.Foes[0].X, 3);
        Assert.Equal(1000, far.Heroes[0].Health);
    }

    [Fact]
    public void A_StruckFoe_ChasesTheArcherFromAnyDistance()
    {
        var archer = Fighter(HeroClass.Archer, health: 1000, damage: 1) with { Arrows = 5 };
        var simulation = new DungeonSimulation(Corridor("......r."), [archer]);
        simulation.MoveTo(archer.Id, 2.5, 1.5);
        Run(simulation, 1);
        simulation.Attack(archer.Id, 0);

        Run(simulation, 3);

        Assert.Equal(archer.Id, simulation.Foes[0].Target);
        Assert.True(simulation.Foes[0].X < 7.5);
        Assert.True(simulation.Heroes[0].Arrows < 5);
    }

    [Fact]
    public void A_SlainFoe_RewardsEveryHeroStillFighting_ButNotTheTsar()
    {
        var killer = Fighter(HeroClass.Spearman, damage: 100);
        var friend = Fighter();
        var tsar = Fighter(HeroClass.Voivode);
        var simulation = new DungeonSimulation(Corridor("........"), [killer, friend, tsar]);
        var far = new DungeonSimulation(Corridor(".r......"), [killer]);

        far.Attack(killer.Id, 0);
        Run(far, 2, new LuckyRandom());

        Assert.Equal(DungeonOutcome.Victory, far.Outcome);
        Assert.Equal(1, far.Heroes[0].Kills);
        Assert.Equal(DungeonCatalog.Foe(EnemyKind.Rat).Experience, far.Heroes[0].Experience);
        Assert.Equal(1, far.Heroes[0].Loot[ResourceKind.Food]);

        var shared = new DungeonSimulation(Corridor(".r......"), [killer, friend, tsar]);
        shared.Attack(killer.Id, 0);
        Run(shared, 2, new LuckyRandom());

        Assert.Equal(shared.Heroes[0].Experience, shared.Heroes[1].Experience);
        Assert.NotEmpty(shared.Heroes[1].Loot);
        Assert.Equal(0, shared.Heroes[2].Experience);
        Assert.Empty(shared.Heroes[2].Loot);
        Assert.Equal(DungeonOutcome.Pending, simulation.Outcome);
    }

    [Fact]
    public void A_FallenHero_FleesToTheEntrance_AndIsOut()
    {
        var weak = Fighter(health: 1);
        var strong = Fighter(health: 10_000);
        var simulation = new DungeonSimulation(Corridor(".r......"), [weak, strong]);

        Run(simulation, 3);

        var fallen = simulation.Hero(weak.Id)!;
        Assert.Equal(HeroState.Left, fallen.State);
        Assert.Equal(EnemyKind.Rat, fallen.Killer);
        Assert.True(simulation.OutcomeOf(fallen).KnockedOut);
        Assert.Equal(strong.Id, simulation.Foes[0].Target);
    }

    [Fact]
    public void TheRun_IsLost_WhenEveryHeroIsOut()
    {
        var simulation = new DungeonSimulation(Corridor(".r......"), [Fighter(health: 1)]);

        Run(simulation, 3);

        Assert.Equal(DungeonOutcome.Defeat, simulation.Outcome);
        Assert.False(simulation.TimedOut);
    }

    [Fact]
    public void TheRun_IsLost_WhenTheTimeRunsOut_WithNoWounds()
    {
        var simulation = new DungeonSimulation(Corridor(".......r", timeLimitSeconds: 1), [Fighter()]);

        Run(simulation, 2);

        Assert.Equal(DungeonOutcome.Defeat, simulation.Outcome);
        Assert.True(simulation.TimedOut);
        Assert.False(simulation.OutcomeOf(simulation.Heroes[0]).KnockedOut);
    }

    [Fact]
    public void A_Healer_TendsAHurtAlly()
    {
        var healer = Fighter(HeroClass.Healer) with { CanHeal = true };
        var hurt = Fighter(health: 50);
        var simulation = new DungeonSimulation(Corridor("........"), [healer, hurt]);
        simulation.Hero(hurt.Id)!.Health = 10;

        simulation.Heal(healer.Id, hurt.Id);
        Run(simulation, 0.2);

        Assert.Equal(10 + HeroClassCatalog.Get(HeroClass.Healer).Heal, simulation.Hero(hurt.Id)!.Health);
        Assert.True(simulation.Hero(healer.Id)!.Healed);
    }

    [Fact]
    public void Settle_StoresTheLoot_WoundsTheFallen_AndSendsThemHome()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village, (Stat.Strength, 2));
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);
        DungeonRules.Join(run, village, citizen, HeroClass.Brawler, Now);
        DungeonRules.March(run, village, [citizen], Now + DungeonRules.Gathering);
        var iron = village.Stock(ResourceKind.IronOre);
        var outcome = new DungeonHeroOutcome(1, 20, [new ResourceAmount(ResourceKind.IronOre, 2)], 1, 0, false, true, EnemyKind.Ghoul);

        DungeonRules.Settle(run, run.Hero(citizen.Id)!, citizen, village, outcome, new Random(1), Now);

        Assert.Equal(iron + 2, village.Stock(ResourceKind.IronOre));
        Assert.Equal(Now + TimeSpan.FromMinutes(EnemyCatalog.Get(EnemyKind.Ghoul).InjuryMinutes), citizen.InjuredUntil);
        Assert.Null(citizen.DungeonRunId);
        Assert.Null(citizen.IdleSpot);
        Assert.True(citizen.SkillLevel(Skill.Guarding) > 0 || citizen.Skills.Any(s => s.Kind == Skill.Guarding && s.Experience > 0));
        Assert.True(run.Hero(citizen.Id)!.Settled);
    }

    [Fact]
    public void March_SendsEveryoneToTheEdge_AndTheTsarToo()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);
        DungeonRules.Join(run, village, citizen, HeroClass.Brawler, Now);
        var closes = run.GatheringEndsAt;

        DungeonRules.March(run, village, [citizen], closes);

        Assert.Equal(DungeonPhase.Marching, run.Phase);
        Assert.Equal(DungeonRules.MarchSpot, citizen.Trip!.To);
        Assert.Equal(DungeonRules.MarchSpot, village.TsarSpot);
        Assert.True(run.MarchEndsAt <= closes + DungeonRules.LongestMarch);
    }
}
