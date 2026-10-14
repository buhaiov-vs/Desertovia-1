using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class StoryQuestTests
{
    private static WorkContext Context(Village village) => new() { Village = village, RecentCitizenCount = 1 };

    private static CycleReport Work(Citizen citizen, string taskId, WorkContext context, ref DateTimeOffset now)
    {
        CycleEngine.Start(citizen, Task(taskId), context, now);
        now = citizen.CycleEndsAt!.Value;
        return CycleEngine.Settle(citizen, context, now)!;
    }

    [Fact]
    public void Chain_OpensOneQuestAtATime_InOrder()
    {
        var village = NewVillage();

        var first = StoryQuests.Open(village, [], [], Now);
        Assert.NotNull(first);
        Assert.Equal(StoryQuests.Chain[0], first.Kind);
        Assert.Null(StoryQuests.Open(village, [first], [], Now));
        Assert.Null(StoryQuests.Open(village, [], StoryQuests.Chain.ToList(), Now));
    }

    [Fact]
    public void EveryStoryQuest_HasAReward()
    {
        Assert.All(StoryQuests.Chain, kind => Assert.True(QuestRules.Rewards.ContainsKey(kind)));
    }

    private static VillageQuest OpenAfter(Village village, QuestKind kind)
    {
        var done = StoryQuests.Chain.TakeWhile(k => k != kind).ToList();
        var quest = StoryQuests.Open(village, [], done, Now)!;
        Assert.Equal(kind, quest.Kind);
        return quest;
    }

    private static bool Done(VillageQuest quest, Village village, string id) =>
        QuestRules.Objectives(quest, village, 1).Single(o => o.Id == id).Done;

    [Fact]
    public void Shelter_NeedsABuiltLeanTo_AndCreditsGatherersAndBuilders()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Wood, 20);
        village.Store(ResourceKind.Reeds, 20);
        var citizen = NewCitizen(village);
        var quest = OpenAfter(village, QuestKind.Shelter);
        Assert.False(Done(quest, village, "plan-lean-to"));

        ConstructionRules.Place(village, BuildingKind.LeanTo, 9, 11, Now);
        Assert.True(Done(quest, village, "plan-lean-to"));
        Assert.False(Done(quest, village, "build-lean-to"));

        var context = Context(village);
        var now = Now;
        QuestRules.Credit(village, [quest], Work(citizen, "cut-reeds", context, ref now));
        Assert.Equal(1, quest.Contributions.Sum(c => c.Amount));

        village.Sites.Clear();
        village.Structures.Add(new Structure { Kind = BuildingKind.LeanTo, PlotId = "9,11", BuiltAt = now });
        Assert.True(QuestRules.IsComplete(quest, village, 1));
    }

    [Fact]
    public void Stores_NeedsFoodInStorage_AndCreditsWhoBroughtIt()
    {
        var village = NewVillage(fed: false);
        var citizen = NewCitizen(village);
        var quest = OpenAfter(village, QuestKind.Stores);
        Assert.Equal(StoryQuests.StoresFood, quest.Target);

        var now = Now;
        QuestRules.Credit(village, [quest], Work(citizen, "forage", Context(village), ref now));
        Assert.Equal(citizen.Id, quest.Contributions.Single().CitizenId);
        Assert.False(QuestRules.IsComplete(quest, village, 1));

        village.Store(ResourceKind.Food, StoryQuests.StoresFood);
        Assert.True(QuestRules.IsComplete(quest, village, 1));
    }

    [Fact]
    public void FirstHouse_NeedsABuiltHut()
    {
        var village = NewVillage();
        var quest = OpenAfter(village, QuestKind.FirstHouse);
        Assert.False(QuestRules.IsComplete(quest, village, 1));

        village.Structures.Add(new Structure { Kind = BuildingKind.Hut, PlotId = "9,11", BuiltAt = Now });
        Assert.True(QuestRules.IsComplete(quest, village, 1));
    }

    [Fact]
    public void Monument_StartsItsProject_WhichNeedsNoEngineer()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var quest = OpenAfter(village, QuestKind.Monument);

        var definition = StoryQuests.ProjectToStart([quest], null, []);
        Assert.Equal(ProjectCatalog.TsarMonument, definition?.Id);
        Assert.False(definition!.NeedsEngineer);
        Assert.Null(StoryQuests.ProjectToStart([quest], VillageProject.Start(village.Id, definition, Now), []));
        Assert.Null(StoryQuests.ProjectToStart([quest], null, [ProjectCatalog.TsarMonument]));

        var project = VillageProject.Start(village.Id, definition, Now);
        Assert.Equal(ProjectPhase.Construction, project.Phase);
        foreach (var material in definition.Materials)
        {
            project.Record(Guid.NewGuid(), material.Resource.ToString(), material.Amount);
        }

        var context = new WorkContext { Village = village, Project = project, RecentCitizenCount = 1, ActiveCitizenCount = 1 };
        var now = Now;
        var report = Work(citizen, "build", context, ref now);
        Assert.True(report.ProjectContribution > 0);

        QuestRules.Credit(village, [quest], report);
        Assert.Single(quest.Contributions);
        Assert.False(QuestRules.IsComplete(quest, village, 1, []));
        Assert.True(QuestRules.IsComplete(quest, village, 1, [ProjectCatalog.TsarMonument]));
    }

    [Fact]
    public void FishingSpot_LocksFishingUntilThePierStands_AndCountsCatches()
    {
        var village = NewVillage();
        var fisher = NewCitizen(village);
        var quest = OpenAfter(village, QuestKind.FishingSpot);
        Assert.Equal(ProjectCatalog.FishingPier, StoryQuests.ProjectToStart([quest], null, [])?.Id);

        var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(fisher, Task(StoryQuests.Fish), Context(village), Now));
        Assert.Equal("task.needsProject", error.Code);

        var withPier = new WorkContext { Village = village, RecentCitizenCount = 1, CompletedProjects = new HashSet<string> { ProjectCatalog.FishingPier } };
        var now = Now;
        for (var cycle = 0; cycle < StoryQuests.FishCatches; cycle++)
        {
            QuestRules.Credit(village, [quest], Work(fisher, StoryQuests.Fish, withPier, ref now));
        }

        Assert.False(QuestRules.IsComplete(quest, village, 1, []));
        Assert.True(QuestRules.IsComplete(quest, village, 1, [ProjectCatalog.FishingPier]));
    }

    [Fact]
    public void Palisade_StartsAWoodOnlyProject_AndCreditsItsBuilders()
    {
        var village = NewVillage();
        var builder = NewCitizen(village);
        var quest = OpenAfter(village, QuestKind.Palisade);

        var definition = StoryQuests.ProjectToStart([quest], null, [ProjectCatalog.TsarMonument, ProjectCatalog.FishingPier]);
        Assert.Equal(ProjectCatalog.Palisade, definition?.Id);
        Assert.False(definition!.NeedsEngineer);
        Assert.Equal([ResourceKind.Wood], definition.Materials.Select(m => m.Resource));

        var project = VillageProject.Start(village.Id, definition, Now);
        project.Record(Guid.NewGuid(), nameof(ResourceKind.Wood), definition.Materials[0].Amount);
        var context = new WorkContext { Village = village, Project = project, RecentCitizenCount = 1, ActiveCitizenCount = 1 };
        var now = Now;
        QuestRules.Credit(village, [quest], Work(builder, "build", context, ref now));

        Assert.Single(quest.Contributions);
        Assert.False(QuestRules.IsComplete(quest, village, 1, []));
        Assert.True(QuestRules.IsComplete(quest, village, 1, [ProjectCatalog.Palisade]));
    }

    [Fact]
    public void TsarBridge_UsesFirstAgeMaterials_AndNeedsAnEngineer()
    {
        var village = NewVillage();
        var quest = OpenAfter(village, QuestKind.TsarBridge);

        var definition = StoryQuests.ProjectToStart([quest], null, []);
        Assert.Equal(ProjectCatalog.TsarBridge, definition?.Id);
        Assert.True(definition!.NeedsEngineer);
        Assert.Equal(
            [ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Reeds],
            definition.Materials.Select(m => m.Resource));

        Assert.True(StoryQuests.Helps(quest, village, Report("supervise", overflowed: false)));
        Assert.False(StoryQuests.Helps(quest, village, Report("supervise", overflowed: true)));
        Assert.False(StoryQuests.Helps(quest, village, Report("measure-river", overflowed: false)));
        Assert.False(QuestRules.IsComplete(quest, village, 1, []));
        Assert.True(QuestRules.IsComplete(quest, village, 1, [ProjectCatalog.TsarBridge]));
    }

    [Fact]
    public void MonksGold_NeedsTheBridge_CountsDigs_AndEmptiesTheVein()
    {
        var village = NewVillage();
        var digger = NewCitizen(village);
        var quest = OpenAfter(village, QuestKind.MonksGold);
        Assert.Null(StoryQuests.ProjectToStart([quest], null, [ProjectCatalog.TsarBridge]));

        var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(digger, Task(GoldVein.DigTask), Context(village), Now));
        Assert.Equal("task.needsProject", error.Code);

        var afterBridge = new WorkContext { Village = village, RecentCitizenCount = 1, CompletedProjects = new HashSet<string> { ProjectCatalog.TsarBridge } };
        var now = Now;
        var report = Work(digger, GoldVein.DigTask, afterBridge, ref now);
        Assert.Equal(ResourceKind.Gold, Assert.Single(report.Produced).Resource);
        Assert.Equal(report.Produced[0].Amount, village.GoldVeinMined);
        QuestRules.Credit(village, [quest], report);
        Assert.Single(quest.Contributions);

        village.GoldVeinMined = GoldVein.Size - 1;
        var last = Work(digger, GoldVein.DigTask, afterBridge, ref now);
        Assert.Equal(1, last.Produced.Sum(p => p.Amount));
        Assert.True(GoldVein.IsEmpty(village));

        var empty = Assert.Throws<GameRuleException>(() => CycleEngine.Start(digger, Task(GoldVein.DigTask), afterBridge, now));
        Assert.Equal("task.veinEmpty", empty.Code);
    }

    [Theory]
    [InlineData(QuestKind.MonasteryBell, ProjectCatalog.BellTower, new[] { ResourceKind.Stone, ResourceKind.Wood, ResourceKind.Gold })]
    [InlineData(QuestKind.GreatGranary, ProjectCatalog.GreatGranary, new[] { ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Reeds })]
    public void GreatWorks_StartTheirProject_WithFirstAgeMaterials(QuestKind kind, string projectId, ResourceKind[] materials)
    {
        var village = NewVillage();
        var quest = OpenAfter(village, kind);

        var definition = StoryQuests.ProjectToStart([quest], null, [ProjectCatalog.TsarBridge]);
        Assert.Equal(projectId, definition?.Id);
        Assert.True(definition!.NeedsEngineer);
        Assert.Equal(materials, definition.Materials.Select(m => m.Resource));
        Assert.True(StoryQuests.Helps(quest, village, Report("supervise", overflowed: false)));
        Assert.True(QuestRules.IsComplete(quest, village, 1, [projectId]));
    }

    [Fact]
    public void MeasuringTheRiver_GivesTheStatsOfAnEngineerWithinADayOfEnergy()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var context = Context(village);
        var now = Now;
        var cycles = 0;
        var engineer = GameCatalog.Roles.Single(r => r.Id == "engineer");

        while (!SkillCatalog.Get(engineer.Skill!.Value).Allows(citizen.Stats, engineer.SkillLevel) && cycles < 200)
        {
            Work(citizen, "measure-river", context, ref now);
            cycles++;
        }

        Assert.InRange(cycles, 1, (int)Energy.Maximum(0));
        Assert.True((now - Now).TotalHours < 2);
    }

    private static CycleReport Report(string taskId, bool overflowed) =>
        new(Guid.NewGuid(), "Test", taskId, [], [], 0, 0, false,
            Stat.Intelligence, 10, Stat.Charisma, 3, Skill.Scholarship, 10, 0, 0, 0, false, overflowed);

    [Fact]
    public void Fire_CountsTendFireCycles_AndRewardsTheKeepers()
    {
        var village = NewVillage();
        var keeper = NewCitizen(village, "Keeper", Now);
        var chopper = NewCitizen(village, "Chopper", Now.AddMinutes(1));
        var quest = StoryQuests.Open(village, [], [], Now)!;
        var context = Context(village);
        var now = Now;

        QuestRules.Credit(village, [quest], Work(chopper, "chop-wood", context, ref now));
        for (var cycle = 0; cycle < StoryQuests.FireCycles - 1; cycle++)
        {
            QuestRules.Credit(village, [quest], Work(keeper, StoryQuests.TendFire, context, ref now));
        }

        Assert.False(QuestRules.IsComplete(quest, village, 2));
        QuestRules.Credit(village, [quest], Work(keeper, StoryQuests.TendFire, context, ref now));
        Assert.True(QuestRules.IsComplete(quest, village, 2));

        var morale = village.MoraleAt(now);
        var completion = QuestRules.Complete(quest, village, new Dictionary<Guid, Citizen> { [keeper.Id] = keeper, [chopper.Id] = chopper }, now);
        Assert.Equal(QuestRules.Rewards[QuestKind.Fire].RenownPool, completion.RenownByCitizen[keeper.Id]);
        Assert.False(completion.RenownByCitizen.ContainsKey(chopper.Id));
        Assert.Equal(Math.Min(GameRules.MoraleMaximum, morale + QuestRules.Rewards[QuestKind.Fire].Morale), village.Morale, 6);
    }
}
