using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class CycleEngineTests
{
    private static (Citizen Citizen, Village Village) Setup(params (Stat, int)[] stats)
    {
        var village = NewVillage();
        return (NewCitizen(village, stats), village);
    }

    [Fact]
    public void NewCitizen_StartsWithEveryStatAtZero()
    {
        var citizen = CharacterCreation.Create(Guid.NewGuid(), Guid.NewGuid(), "Bob", Now);

        Assert.Equal(0, citizen.Stats.Total);
        Assert.Equal(GameRules.FirstLevelExperience, Progression.Curve(0));
        Assert.Throws<GameRuleException>(() => CharacterCreation.Create(Guid.NewGuid(), Guid.NewGuid(), " ", Now));
    }

    [Fact]
    public void NewCitizen_KeepsAValidLook_AndRejectsAnUnknownOne()
    {
        var look = new Appearance { Gender = Gender.Female, Style = 1, SkinTone = 3, HairColor = 5, OutfitColor = 2 };
        var citizen = CharacterCreation.Create(Guid.NewGuid(), Guid.NewGuid(), "Ann", Now, look);

        Assert.Same(look, citizen.Appearance);

        // Women have fewer styles than men.
        var missingStyle = new Appearance { Gender = Gender.Female, Style = Appearance.FemaleStyles };
        Assert.Throws<GameRuleException>(() => CharacterCreation.Create(Guid.NewGuid(), Guid.NewGuid(), "Ann", Now, missingStyle));
        var missingSkin = new Appearance { SkinTone = -1 };
        Assert.Throws<GameRuleException>(() => CharacterCreation.Create(Guid.NewGuid(), Guid.NewGuid(), "Ann", Now, missingSkin));
    }

    [Fact]
    public void Start_BeforeCycleEnds_IsRejected()
    {
        var (citizen, village) = Setup();
        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);

        Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now.AddSeconds(30)));
    }

    [Fact]
    public void Start_WithoutTheSkill_IsRejected()
    {
        var (citizen, village) = Setup();

        var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("survey"), WorkContext.For(village), Now));
        Assert.Equal("task.skillRequired", error.Code);
    }

    [Fact]
    public void Start_WithoutItsBuilding_IsRejected()
    {
        var (citizen, village) = Setup((Stat.Strength, 4));
        village.Research.Add(new TechnologyProgress { TechnologyId = TechnologyCatalog.Mining, ResearchedAt = Now });

        var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("mine-coal"), WorkContext.For(village), Now));
        Assert.Equal("task.needsBuilding", error.Code);

        village.Structures.Add(new Structure { Kind = BuildingKind.Mine, PlotId = "9,11" });
        CycleEngine.Start(citizen, Task("mine-coal"), WorkContext.For(village), Now);
    }

    [Fact]
    public void Duration_IsWorkPlusTheWalkFromTheForestToStorage()
    {
        var (citizen, village) = Setup();
        var forest = WorldMap.Places["forest"];

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);

        var walk = WorldMap.Walk(forest, village.DropPoint(ResourceKind.Wood, forest));
        Assert.True(walk > TimeSpan.Zero);
        Assert.Equal(Now + GameRules.CycleLength + walk, citizen.CycleEndsAt);
    }

    [Fact]
    public void Settle_ProducesResourcesAndExperience()
    {
        var (citizen, village) = Setup();
        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);

        Assert.Null(CycleEngine.Settle(citizen, WorkContext.For(village), citizen.CycleEndsAt!.Value.AddSeconds(-1)));
        var report = Finish(citizen, WorkContext.For(village));

        Assert.True(village.Stock(ResourceKind.Wood) > 0);
        Assert.Equal(10, report.PrimaryExperience);
        Assert.Equal(3, report.SecondaryExperience);
        Assert.False(report.Hungry);
        Assert.Null(CycleEngine.Settle(citizen, WorkContext.For(village), citizen.CycleEndsAt!.Value.AddSeconds(1)));
    }

    [Fact]
    public void RenewInsideGraceWindow_KeepsStreak_OutsideResetsIt()
    {
        var (citizen, village) = Setup();
        var chop = Task("chop-wood");

        CycleEngine.Start(citizen, chop, WorkContext.For(village), Now);
        Finish(citizen, WorkContext.For(village));
        CycleEngine.Start(citizen, chop, WorkContext.For(village), citizen.CycleEndsAt!.Value.AddSeconds(30));
        Assert.Equal(1, citizen.Streak);

        Finish(citizen, WorkContext.For(village));
        CycleEngine.Start(citizen, chop, WorkContext.For(village), citizen.CycleEndsAt!.Value + GameRules.GraceWindow + TimeSpan.FromSeconds(1));
        Assert.Equal(0, citizen.Streak);
    }

    [Fact]
    public void TiredCitizen_GainsNoExperience_AndLessYield()
    {
        var (citizen, village) = Setup();
        citizen.StoredEnergy = 0;
        citizen.EnergyUpdatedAt = Now;

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        var report = Finish(citizen, WorkContext.For(village));

        Assert.True(report.Tired);
        Assert.Equal(0, report.PrimaryExperience);
        Assert.Equal(1, village.Stock(ResourceKind.Wood));
    }

    [Fact]
    public void FullStorage_WorkerKeepsTheLoad_AndHalvesExperience()
    {
        var (citizen, village) = Setup();
        village.Store(ResourceKind.Wood, 1000);

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        var report = Finish(citizen, WorkContext.For(village));

        Assert.True(report.Overflowed);
        Assert.Equal(5, report.PrimaryExperience);
        Assert.Equal(report.Produced.Single().Amount, citizen.Carried(ResourceKind.Wood));
        Assert.True(citizen.IsCarrying);
    }

    [Fact]
    public void PartlyFullStorage_ScalesExperienceByStoredShare()
    {
        var (citizen, village) = Setup();
        village.Store(ResourceKind.Wood, village.FreeSpace(ResourceKind.Wood) - 1);

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        var report = Finish(citizen, WorkContext.For(village));

        var produced = report.Produced.Single().Amount;
        Assert.Equal(produced - 1, report.Carried.Single().Amount);
        Assert.Equal(YieldMath.Whole(10 * (0.5 + 0.5 / produced)), report.PrimaryExperience);
    }

    [Fact]
    public void FullHandsAndStorage_BlockTheSameGood_ButNotOtherJobs()
    {
        var (citizen, village) = Setup();
        citizen.Carry(ResourceKind.Wood, StorageRules.TakeLimit);
        village.Store(ResourceKind.Wood, 1000);

        var error = Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now));
        Assert.Equal("hands.full", error.Code);

        CycleEngine.Start(citizen, Task("quarry-stone"), WorkContext.For(village), Now);
    }

    [Fact]
    public void FullStorage_HandsTakeUpToTheLimit_AndTheRestIsLost()
    {
        var (citizen, village) = Setup();
        citizen.Carry(ResourceKind.Wood, StorageRules.TakeLimit - 1);
        village.Store(ResourceKind.Wood, 1000);

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        var report = Finish(citizen, WorkContext.For(village));

        var produced = report.Produced.Single().Amount;
        Assert.Equal(StorageRules.TakeLimit, citizen.Carried(ResourceKind.Wood));
        Assert.Equal(1, report.Carried.Single().Amount);
        Assert.Equal(produced - 1, report.Lost.Single().Amount);
        Assert.True(report.Overflowed);
    }

    [Fact]
    public void Carrying_BlocksExpeditions()
    {
        var (citizen, village) = Setup();
        citizen.WithSkill(Skill.Exploring, Task("expedition").SkillLevel).Carry(ResourceKind.Stone, 1);

        var error = Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("expedition"), WorkContext.For(village), Now));
        Assert.Equal("hands.fullExpedition", error.Code);
    }

    [Fact]
    public void Carrying_BlocksRoaming()
    {
        var (citizen, village) = Setup();
        citizen.Carry(ResourceKind.Stone, 1);

        var error = Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("roam"), WorkContext.For(village), Now));
        Assert.Equal("hands.fullRoam", error.Code);
    }

    [Fact]
    public void Roaming_FindsOnlyCommonGoods_AndSometimesNothing()
    {
        var (citizen, village) = Setup();
        var context = new WorkContext { Village = village, Random = new Random(7) };
        var common = Roaming.Table.Select(e => e.Resource).ToHashSet();
        var empty = 0;
        var found = 0;

        for (var cycle = 0; cycle < 200; cycle++)
        {
            citizen.StoredEnergy = 100;
            citizen.DropLoad();
            var start = citizen.CycleEndsAt ?? Now;
            var work = SkillRules.WorkTime(citizen, Skill.Exploring, 0, GameRules.CycleLength);
            CycleEngine.Start(citizen, Task("roam"), context, start);
            Assert.Equal(start + work, citizen.CycleEndsAt);

            var report = Finish(citizen, context);
            Assert.All(report.Produced, p => Assert.Contains(p.Resource, common));
            Assert.Equal(report.Produced.Count, report.Produced.Select(p => p.Resource).Distinct().Count());
            empty += report.Produced.Count == 0 ? 1 : 0;
            found += report.Produced.Count;
        }

        Assert.InRange(empty, 1, 60);
        Assert.True(found > 100);
    }

    [Fact]
    public void Roaming_FindsMoreWithAKeenEye()
    {
        Assert.Equal(0.25, Roaming.FindChance(0), 6);
        Assert.Equal(0.5, Roaming.FindChance(GameRules.StatMaximum), 6);
    }

    [Fact]
    public void Load_StaysInHands_UntilTheCitizenCarriesItToStorage()
    {
        var (citizen, village) = Setup();
        citizen.Carry(ResourceKind.Wood, 3);

        CycleEngine.Start(citizen, Task("quarry-stone"), WorkContext.For(village), Now);
        Finish(citizen, WorkContext.For(village));
        Assert.Equal(3, citizen.Carried(ResourceKind.Wood));

        var start = citizen.CycleEndsAt!.Value;
        CycleEngine.Start(citizen, Task(GameCatalog.CarryToStorage), WorkContext.For(village), start);
        Assert.True(citizen.CycleEndsAt > start + TimeSpan.FromSeconds(Task(GameCatalog.CarryToStorage).WorkSeconds));
        var report = Finish(citizen, WorkContext.For(village));

        Assert.False(citizen.IsCarrying);
        Assert.Equal(3, report.Produced.Single().Amount);
        Assert.Equal(3, village.Stock(ResourceKind.Wood));
    }

    [Fact]
    public void CarryToStorage_NeedsALoadAndRoom()
    {
        var (citizen, village) = Setup();
        var carry = Task(GameCatalog.CarryToStorage);

        Assert.Equal("hands.empty",
            Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, carry, WorkContext.For(village), Now)).Code);

        village.Store(ResourceKind.Wood, 1000);
        citizen.Carry(ResourceKind.Wood, 3);
        Assert.Equal("hands.noRoom",
            Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, carry, WorkContext.For(village), Now)).Code);

        citizen.DropLoad();
        Assert.Empty(citizen.Load);
        Assert.False(citizen.IsCarrying);
    }

    private static CycleReport Work(Citizen citizen, Village village)
    {
        CycleEngine.Start(citizen, Task("quarry-stone"), WorkContext.For(village), citizen.CycleEndsAt ?? Now);
        return Finish(citizen, WorkContext.For(village));
    }

    [Fact]
    public void Citizens_EatOneFoodEveryFewCycles_AndAnEmptyLarderMakesThemHungry()
    {
        var village = NewVillage(fed: false);
        var citizen = NewCitizen(village);

        for (var cycle = 1; cycle < GameRules.CyclesPerMeal; cycle++)
        {
            Assert.False(Work(citizen, village).Hungry);
        }

        var hungry = Work(citizen, village);
        Assert.True(hungry.Hungry);
        Assert.True(citizen.IsHungry);
        Assert.Equal(5, hungry.PrimaryExperience);

        // A hungry citizen looks for food after every cycle, not only at mealtime.
        village.Store(ResourceKind.Food, 1);
        var fed = Work(citizen, village);
        Assert.False(fed.Hungry);
        Assert.False(citizen.IsHungry);
        Assert.Equal(0, village.Stock(ResourceKind.Food));
        Assert.Equal(0, citizen.CyclesSinceMeal);
    }

    [Fact]
    public void Eating_TakesFoodFromHandsBeforeStorage()
    {
        var village = NewVillage(fed: false);
        village.Store(ResourceKind.Food, 1);
        var citizen = NewCitizen(village);
        citizen.Carry(ResourceKind.Food, 1);
        citizen.CyclesSinceMeal = GameRules.CyclesPerMeal - 1;

        var report = Work(citizen, village);

        Assert.False(report.Hungry);
        Assert.False(citizen.IsHungry);
        Assert.Equal(0, citizen.Carried(ResourceKind.Food));
        Assert.Equal(1, village.Stock(ResourceKind.Food));
    }

    [Fact]
    public void Sleeping_UnderTheTrees_RegeneratesHalfAsFast()
    {
        var village = NewVillage();
        var citizens = Enumerable.Range(0, 5).Select(i => NewCitizen(village, $"C{i}", Now.AddMinutes(i))).ToList();
        village.Structures.Add(new Structure { Kind = BuildingKind.Hut, PlotId = "9,11" });
        ShelterRules.Assign(village, citizens, Now);
        var inside = citizens[0];
        var outside = citizens[4];
        foreach (var citizen in new[] { outside, inside })
        {
            citizen.StoredEnergy = 0;
            citizen.EnergyUpdatedAt = Now;
        }

        Assert.Equal(1, inside.ShelterQuality);
        Assert.Equal(GameRules.UnderTreesRegeneration, outside.ShelterQuality);
        var later = Now.AddHours(1);
        Assert.Equal(inside.CurrentEnergy(later, village) * GameRules.UnderTreesRegeneration, outside.CurrentEnergy(later, village), 6);
    }

    [Fact]
    public void Inputs_AreConsumedAtStart_AndRequired()
    {
        var (citizen, village) = Setup((Stat.Strength, 4), (Stat.Dexterity, 3));
        village.Structures.Add(new Structure { Kind = BuildingKind.Smithy, PlotId = "9,11" });
        village.Research.Add(new TechnologyProgress { TechnologyId = TechnologyCatalog.Smelting, ResearchedAt = Now });
        var smelt = Task("smelt-ingot");

        Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, smelt, WorkContext.For(village), Now));

        village.Store(ResourceKind.IronOre, 1);
        citizen.Carry(ResourceKind.Coal, 1);
        CycleEngine.Start(citizen, smelt, WorkContext.For(village), Now);

        Assert.Equal(0, village.Stock(ResourceKind.IronOre));
        Assert.Equal(0, citizen.Carried(ResourceKind.Coal));
    }

    [Fact]
    public void CraftsWithMasterWork_HaveFullExperienceTaskAtEveryLevel()
    {
        var available = GameCatalog.Tasks.Where(t => t.Effect != TaskEffect.NotAvailableYet).ToList();
        var crafts = available.Where(t => t.SkillLevel == GameCatalog.MasterSkill).Select(t => t.Skill).Distinct().ToList();
        Assert.NotEmpty(crafts);

        foreach (var skill in crafts)
        {
            var requirements = available.Where(t => t.Skill == skill).Select(t => t.SkillLevel).ToHashSet();
            for (var level = 0; level < GameRules.SkillMaximum; level++)
            {
                Assert.True(
                    requirements.Any(r => r <= level && Progression.ChallengeMultiplier(level, r) == 1.0),
                    $"{skill} has no full-experience task at level {level}.");
            }
        }
    }

    [Fact]
    public void FarTooEasyWork_TeachesLittle()
    {
        var village = NewVillage();
        var novice = NewCitizen(village);
        var woodcutter = NewCitizen(village).WithSkill(Skill.Woodcutting, GameRules.ChallengeThreshold + 1);

        CycleEngine.Start(novice, Task("chop-wood"), WorkContext.For(village), Now);
        CycleEngine.Start(woodcutter, Task("chop-wood"), WorkContext.For(village), Now);

        var full = Finish(novice, WorkContext.For(village)).PrimaryExperience;
        var penalized = Finish(woodcutter, WorkContext.For(village)).PrimaryExperience;
        Assert.Equal(YieldMath.Whole(full * GameRules.ChallengePenalty), penalized);
    }
}
