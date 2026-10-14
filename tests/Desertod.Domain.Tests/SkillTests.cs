using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Technologies;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class SkillTests
{
    private static Village BakeryVillage()
    {
        var village = NewVillage();
        foreach (var technology in new[] { TechnologyCatalog.Agriculture, TechnologyCatalog.Baking })
        {
            village.Research.Add(new TechnologyProgress { TechnologyId = technology, ResearchedAt = Now });
        }

        village.Structures.Add(new Structure { Kind = BuildingKind.Bakery, PlotId = "21,12", BuiltAt = Now });
        village.Store(ResourceKind.Grain, 10);
        return village;
    }

    private static Citizen Baker(Village village, int bakingLevel = 0) =>
        NewCitizen(village, (Stat.Dexterity, 4), (Stat.Endurance, 2)).WithSkill(Skill.Baking, bakingLevel);

    [Fact]
    public void BakingBread_TrainsTheBakingSkill()
    {
        var village = BakeryVillage();
        var baker = Baker(village);

        CycleEngine.Start(baker, Task("bake-bread"), WorkContext.For(village), Now);
        var report = Finish(baker, WorkContext.For(village));

        Assert.Equal(Skill.Baking, report.Skill);
        Assert.Equal(report.PrimaryExperience, report.SkillExperience);
        Assert.Equal(report.PrimaryExperience, baker.Skills.Single(s => s.Kind == Skill.Baking).Experience);
        Assert.Equal(0, baker.SkillLevel(Skill.Woodcutting));
    }

    [Fact]
    public void ConstructionJobs_TrainTheirCraft()
    {
        Assert.Equal(Skill.Hauling, BuildingCatalog.FindJob("bring-logs")!.Skill);
        Assert.Equal(Skill.Building, BuildingCatalog.FindJob("raise-posts")!.Skill);
        Assert.Equal(Skill.Woodcutting, BuildingCatalog.FindJob(BuildingCatalog.FellTrees)!.Skill);
    }

    [Fact]
    public void Experience_RaisesTheLevel_OnTheStatCurve()
    {
        var citizen = NewCitizen(NewVillage());

        var gained = SkillRules.AddExperience(citizen, Skill.Baking, GameRules.FirstLevelExperience + 10);

        Assert.Equal(1, gained);
        var progress = citizen.Skills.Single();
        Assert.Equal(1, progress.Level);
        Assert.Equal(10, progress.Experience);
    }

    [Fact]
    public void Experience_PastTheMaximum_IsLost()
    {
        var citizen = NewCitizen(NewVillage());
        citizen.Skills.Add(new SkillProgress { Kind = Skill.Baking, Level = GameRules.SkillMaximum });

        Assert.Equal(0, SkillRules.AddExperience(citizen, Skill.Baking, 1000));
        Assert.Equal(0, citizen.Skills.Single().Experience);
        Assert.Equal(0, SkillRules.ExperienceToNext(GameRules.SkillMaximum));
    }

    [Fact]
    public void SkilledBaker_BakesMore_AndFaster()
    {
        var village = BakeryVillage();
        var novice = Baker(village);
        var master = Baker(village, bakingLevel: GameRules.SkillBonusLevels);

        CycleEngine.Start(novice, Task("bake-bread"), WorkContext.For(village), Now);
        CycleEngine.Start(master, Task("bake-bread"), WorkContext.For(village), Now);

        var saved = TimeSpan.FromSeconds(Task("bake-bread").WorkSeconds) * GameRules.SkillWorkTimePerLevel * GameRules.SkillBonusLevels;
        Assert.Equal(novice.CycleEndsAt - saved, master.CycleEndsAt);

        var noviceBread = Finish(novice, WorkContext.For(village)).Produced.Single().Amount;
        var masterBread = Finish(master, WorkContext.For(village)).Produced.Single().Amount;
        Assert.True(masterBread > noviceBread);
    }

    [Fact]
    public void Bonus_StopsGrowing_PastTheBonusLevels()
    {
        Assert.Equal(GameRules.SkillBonusLevels, SkillRules.Mastery(GameRules.SkillMaximum, 0));
        Assert.Equal(0, SkillRules.Mastery(2, 3));
        Assert.Equal(1, SkillRules.Mastery(4, 3));
    }

    [Fact]
    public void MasterWork_NeedsSkillInItsCraft()
    {
        var citizen = NewCitizen(NewVillage());
        var task = Task("mine-gold");

        var error = Assert.Throws<GameRuleException>(() => SkillRules.Check(citizen, task));
        Assert.Equal("task.skillRequired", error.Code);
        Assert.Equal(nameof(Skill.Mining), error.Arguments["skill"]);

        citizen.Skills.Add(new SkillProgress { Kind = Skill.Mining, Level = task.SkillLevel });
        SkillRules.Check(citizen, task);
    }

    [Fact]
    public void MasterTasks_NeedTheMasterLevel_AndEveryCraftHasWorkForNovices()
    {
        foreach (var task in GameCatalog.Tasks)
        {
            var role = GameCatalog.RoleOf(task);
            if (role.Tier == RoleTier.Master)
            {
                Assert.Equal(GameCatalog.MasterSkill, task.SkillLevel);
            }
        }

        foreach (var skill in Enum.GetValues<Skill>())
        {
            Assert.Contains(GameCatalog.Tasks, t => t.Skill == skill && t.SkillLevel == 0);
        }
    }

    [Fact]
    public void EveryRole_HasASkill_ExceptLaborers()
    {
        foreach (var role in GameCatalog.Roles)
        {
            Assert.Equal(role.Tier == RoleTier.Basic, role.Skill is null);
        }
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(2, 1, 0)]
    [InlineData(3, 2, 1)]
    [InlineData(7, 6, 3)]
    [InlineData(10, 9, 4)]
    public void SkillLevels_NeedStats(int level, int primary, int secondary)
    {
        var smithing = SkillCatalog.Get(Skill.Smithing);

        Assert.Equal(new StatRequirement[] { new(Stat.Strength, primary), new(Stat.Dexterity, secondary) }, smithing.RequirementsFor(level));
    }

    [Fact]
    public void EverySkill_RestsOnTwoDifferentStats()
    {
        foreach (var skill in Enum.GetValues<Skill>())
        {
            var definition = SkillCatalog.Get(skill);
            Assert.NotEqual(definition.Primary, definition.Secondary);
        }
    }

    [Fact]
    public void LevelBeyondTheStats_Waits_KeepingOneLevelOfExperience()
    {
        var citizen = NewCitizen(NewVillage());
        SkillRules.AddExperience(citizen, Skill.Smithing, GameRules.FirstLevelExperience);
        Assert.Equal(1, citizen.SkillLevel(Skill.Smithing));

        var gained = SkillRules.AddExperience(citizen, Skill.Smithing, 10_000);

        Assert.Equal(0, gained);
        Assert.Equal(1, citizen.SkillLevel(Skill.Smithing));
        Assert.Equal(SkillRules.ExperienceToNext(1), citizen.Skills.Single().Experience);
        Assert.False(SkillRules.CanReachNext(citizen, Skill.Smithing));
    }

    [Fact]
    public void WaitingLevel_Comes_OnceTheStatsCatchUp()
    {
        var citizen = NewCitizen(NewVillage());
        SkillRules.AddExperience(citizen, Skill.Smithing, GameRules.FirstLevelExperience + SkillRules.ExperienceToNext(1));
        Assert.Equal(1, citizen.SkillLevel(Skill.Smithing));

        citizen.Stats.Set(Stat.Strength, 1);
        SkillRules.CatchUp(citizen);

        Assert.Equal(2, citizen.SkillLevel(Skill.Smithing));
        Assert.Equal(0, citizen.Skills.Single().Experience);
    }

    [Fact]
    public void Working_RaisesAWaitingSkill_WhenItsStatGrows()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        SkillRules.AddExperience(citizen, Skill.Smithing, GameRules.FirstLevelExperience + SkillRules.ExperienceToNext(1));
        citizen.Stats.StrengthExperience = Progression.ExperienceToNext(0) - 1;

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        Finish(citizen, WorkContext.For(village));

        Assert.Equal(1, citizen.Stats.Strength);
        Assert.Equal(2, citizen.SkillLevel(Skill.Smithing));
    }

    [Fact]
    public void TalentCap_LimitsWhichCraftsCanBeMastered()
    {
        var citizen = NewCitizen(NewVillage(), (Stat.Strength, 10), (Stat.Dexterity, 10), (Stat.Endurance, 10));
        Assert.Equal(GameRules.TalentCap, citizen.Stats.Total);

        SkillRules.AddExperience(citizen, Skill.Scholarship, 100_000);
        SkillRules.AddExperience(citizen, Skill.Smithing, 100_000);

        Assert.Equal(1, citizen.SkillLevel(Skill.Scholarship));
        Assert.Equal(GameRules.SkillMaximum, citizen.SkillLevel(Skill.Smithing));
    }
}
