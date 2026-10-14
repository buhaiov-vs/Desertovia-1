using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;

namespace Desertod.Domain.Tests;

public class ProjectTests
{
    private static readonly DateTimeOffset Now = TestWorld.Now;

    private static Citizen NewCitizen(Village village, int intelligence = 1, int strength = 4) =>
        TestWorld.NewCitizen(village, (Stat.Strength, strength), (Stat.Intelligence, intelligence), (Stat.Endurance, 3), (Stat.Dexterity, 2));

    private static (Village Village, VillageProject Project) Setup()
    {
        var village = TestWorld.NewVillage();
        var project = VillageProject.Start(village.Id, ProjectCatalog.Find(ProjectCatalog.TsarBridge)!, Now);
        return (village, project);
    }

    private static TaskDefinition Task(string id) => GameCatalog.FindTask(id)!;

    private static CycleReport Run(Citizen citizen, string taskId, WorkContext context, ref DateTimeOffset now)
    {
        CycleEngine.Start(citizen, Task(taskId), context, now);
        now = citizen.CycleEndsAt!.Value;
        return CycleEngine.Settle(citizen, context, now)!;
    }

    [Fact]
    public void Deliver_MovesResourcesFromStorageIntoProject()
    {
        var (village, project) = Setup();
        village.Store(ResourceKind.Wood, 100);
        var citizen = NewCitizen(village);
        var context = new WorkContext { Village = village, Project = project };
        var now = Now;

        var report = Run(citizen, "deliver-wood", context, ref now);

        Assert.True(report.ProjectContribution > 0);
        Assert.Equal(100 - report.ProjectContribution, village.Stock(ResourceKind.Wood));
        Assert.Equal(report.ProjectContribution, project.Delivered(ResourceKind.Wood));
    }

    [Fact]
    public void Deliver_TakesFromTheCitizensHandsFirst()
    {
        var (village, project) = Setup();
        village.Store(ResourceKind.Wood, 1000);
        var citizen = NewCitizen(village);
        citizen.Carry(ResourceKind.Wood, 2);
        var stock = village.Stock(ResourceKind.Wood);
        var context = new WorkContext { Village = village, Project = project };
        var now = Now;

        var report = Run(citizen, "deliver-wood", context, ref now);

        Assert.False(citizen.IsCarrying);
        Assert.Equal(stock - (report.ProjectContribution - 2), village.Stock(ResourceKind.Wood));
    }

    [Fact]
    public void Deliver_WithOnlyCarriedGoods_IsAllowed()
    {
        var (village, project) = Setup();
        village.Store(ResourceKind.Stone, 1000);
        var citizen = NewCitizen(village);
        citizen.Carry(ResourceKind.Wood, 3);

        CycleEngine.Start(citizen, Task("deliver-wood"), new WorkContext { Village = village, Project = project }, Now);
    }

    [Fact]
    public void Deliver_WithEmptyStorage_IsRejected()
    {
        var (village, project) = Setup();
        var citizen = NewCitizen(village);

        Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("deliver-wood"), new WorkContext { Village = village, Project = project }, Now));
    }

    [Fact]
    public void Build_BeforeSurvey_IsRejected()
    {
        var (village, project) = Setup();
        var citizen = NewCitizen(village);
        var context = new WorkContext { Village = village, Project = project, SupervisionCapacity = 10 };

        var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("build"), context, Now));
        Assert.Equal("project.surveyFirst", error.Code);
    }

    [Fact]
    public void Build_WithoutSupervisingEngineer_IsRejected()
    {
        var (village, project) = Setup();
        project.SurveyProgress = project.Definition.SurveyCycles;
        project.Materials.ForEach(m => m.Delivered = 1000);
        var citizen = NewCitizen(village);

        var error = Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("build"), new WorkContext { Village = village, Project = project }, Now));
        Assert.Equal("project.needSupervisor", error.Code);
    }

    [Fact]
    public void Build_CannotRunAheadOfMaterials()
    {
        var (village, project) = Setup();
        project.SurveyProgress = project.Definition.SurveyCycles;
        var citizen = NewCitizen(village);
        var context = new WorkContext { Village = village, Project = project, SupervisionCapacity = 10 };

        Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("build"), context, Now));

        project.Materials.ForEach(m => m.Delivered = project.Definition.Materials.Single(d => d.Resource == m.Kind).Amount / 2);
        var now = Now;
        var report = Run(citizen, "build", context, ref now);

        Assert.True(report.ProjectContribution > 0);
        Assert.True(project.BuildProgress <= project.BuildLimit);
    }

    [Fact]
    public void Survey_RequiresEngineer()
    {
        var (village, project) = Setup();
        var laborer = NewCitizen(village, intelligence: 1);
        var engineer = NewCitizen(village, intelligence: 4, strength: 2).WithSkill(Skill.Building, GameCatalog.JourneymanSkill);
        var context = new WorkContext { Village = village, Project = project };

        Assert.Throws<GameRuleException>(() => CycleEngine.Start(laborer, Task("survey"), context, Now));

        var now = Now;
        var report = Run(engineer, "survey", context, ref now);
        Assert.True(project.SurveyProgress > 0);
        Assert.Equal(report.ProjectContribution, project.SurveyProgress);
    }

    [Fact]
    public void ContributionCap_IsTwentyPercent_InBigVillages_AndEqualSplitInSmallOnes()
    {
        Assert.Equal(60, ProjectRules.ContributionCap(300, 10));
        Assert.Equal(150, ProjectRules.ContributionCap(300, 2));
        Assert.Equal(300, ProjectRules.ContributionCap(300, 1));
        Assert.Equal(34, ProjectRules.ContributionCap(100, 3));
    }

    [Fact]
    public void Deliver_StopsAtContributionCap()
    {
        var (village, project) = Setup();
        village.Store(ResourceKind.Wood, 120);
        var citizen = NewCitizen(village);
        var context = new WorkContext { Village = village, Project = project, ActiveCitizenCount = 10 };
        var now = Now;

        for (var cycle = 0; cycle < 30 && project.ContributionRoom(citizen.Id, "Wood", 10) > 0; cycle++)
        {
            Run(citizen, "deliver-wood", context, ref now);
        }

        Assert.Equal(ProjectRules.ContributionCap(project.Required("Wood"), 10), project.Contributed(citizen.Id, "Wood"));
        Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("deliver-wood"), context, now));
    }

    [Fact]
    public void Complete_RewardsVillageAndSplitsRenownByShare()
    {
        var (village, project) = Setup();
        var first = NewCitizen(village);
        var second = NewCitizen(village);
        project.SurveyProgress = project.Definition.SurveyCycles;
        project.BuildProgress = project.Definition.BuildCycles;
        foreach (var material in project.Definition.Materials)
        {
            project.Record(first.Id, material.Resource.ToString(), material.Amount * 3 / 4);
            project.Record(second.Id, material.Resource.ToString(), material.Amount / 4);
        }

        Assert.True(project.IsComplete);

        var completion = ProjectRules.Complete(
            project, village, new Dictionary<Guid, Citizen> { [first.Id] = first, [second.Id] = second }, Now);

        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.Equal(375, completion.RenownByCitizen[first.Id]);
        Assert.Equal(125, completion.RenownByCitizen[second.Id]);
        Assert.Equal(1.1, village.Morale, 6);
    }

    [Fact]
    public void GreatGranary_GivesTheVillageAStorageBuilding()
    {
        var village = TestWorld.NewVillage();
        var project = VillageProject.Start(village.Id, ProjectCatalog.Find("granary")!, Now);

        ProjectRules.Complete(project, village, new Dictionary<Guid, Citizen>(), Now);

        var granary = Assert.Single(village.Storages, s => s.Kind == StorageKind.GreatGranary);
        Assert.Equal("granary", granary.PlotId);
        Assert.True(village.FreeSpace(ResourceKind.Food) >= 600);
    }

    [Fact]
    public void Progress_WeighsSurveyMaterialsAndBuild()
    {
        var (_, project) = Setup();
        Assert.Equal(0, project.Progress, 6);

        project.SurveyProgress = project.Definition.SurveyCycles;
        Assert.Equal(0.1, project.Progress, 6);

        project.Materials.ForEach(m => m.Delivered = 1_000_000);
        project.BuildProgress = project.Definition.BuildCycles;
        Assert.Equal(1.0, project.Progress, 6);
    }

    [Fact]
    public void Expedition_ProducesLoot_AndRareFindsArePossible()
    {
        var village = TestWorld.NewVillage();
        var citizen = NewCitizen(village).WithSkill(Skill.Exploring, Task("expedition").SkillLevel);
        var context = new WorkContext { Village = village, Random = new Random(42) };
        var now = Now;
        var rareFinds = 0;

        for (var cycle = 0; cycle < 300; cycle++)
        {
            citizen.StoredEnergy = 100;
            citizen.DropLoad();
            var report = Run(citizen, "expedition", context, ref now);
            Assert.NotEmpty(report.Produced);
            rareFinds += report.RareFind ? 1 : 0;
        }

        Assert.InRange(rareFinds, 1, 30);
    }
}
