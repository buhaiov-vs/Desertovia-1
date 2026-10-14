using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class ConstructionTests
{
    private static WorkContext Context(Village village, int recent) => new() { Village = village, RecentCitizenCount = recent };

    private static CycleReport Job(Citizen citizen, string plotId, int stage, string jobId, WorkContext context, ref DateTimeOffset now)
    {
        CycleEngine.StartJob(citizen, plotId, stage, jobId, context, now);
        now = citizen.CycleEndsAt!.Value;
        return CycleEngine.Settle(citizen, context, now)!;
    }

    [Fact]
    public void Blueprint_CostsNothing_AndRespectsPlotsAndRequirements()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Wood, 50);

        var site = ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);

        Assert.Equal(50, village.Stock(ResourceKind.Wood));
        Assert.Equal(0, site.Stage);
        Assert.Equal("9,11", site.PlotId);
        Assert.Equal("building.plotTaken",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.Heap, 9, 11, Now)).Code);
        Assert.Equal("building.cannotBuildHere",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.Heap, -5, 0, Now)).Code);
        Assert.Equal("building.requires",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.Hut, 21, 12, Now)).Code);
        Assert.Equal("building.notAvailable",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.Treasury, 21, 12, Now)).Code);
    }

    [Fact]
    public void Blueprint_GoesAnywhereOnFreeLand()
    {
        var village = NewVillage();

        ConstructionRules.Place(village, BuildingKind.WoodPile, 2, 5, Now);
        ConstructionRules.Place(village, BuildingKind.StonePile, 5, 5, Now);

        Assert.Equal("building.plotTaken",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.Heap, 3, 8, Now)).Code);
        Assert.Equal("building.cannotBuildHere",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.Heap, 13, 11, Now)).Code);
        Assert.Equal("building.cannotBuildHere",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.Heap, 26, 5, Now)).Code);
    }

    [Fact]
    public void AtMostThreeSites_AreOpenAtOnce()
    {
        var village = NewVillage();
        ConstructionRules.Place(village, BuildingKind.LeanTo, 9, 11, Now);
        ConstructionRules.Place(village, BuildingKind.LeanTo, 21, 12, Now);
        ConstructionRules.Place(village, BuildingKind.LeanTo, 25, 12, Now);

        Assert.Equal("building.tooManySites",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.LeanTo, 9, 17, Now)).Code);
    }

    [Fact]
    public void Haul_MovesMaterialFromStorage_AndLaborCannotRunAhead()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Wood, 2);
        var citizen = NewCitizen(village);
        ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);
        var context = Context(village, 1);
        var now = Now;

        Assert.Equal("site.waitingMaterial",
            Assert.Throws<GameRuleException>(() => CycleEngine.StartJob(citizen, "9,11", 0, "stack-pile", context, now)).Code);

        var haul = Job(citizen, "9,11", 0, "bring-sticks", context, ref now);

        Assert.Equal(2, haul.ProjectContribution);
        Assert.Equal(0, village.Stock(ResourceKind.Wood));
        var site = village.Sites.Single();
        Assert.Equal(2, site.Done(0, "bring-sticks"));
        Assert.Equal(0, site.LaborRoom(0, "stack-pile"));
        Assert.Equal("site.noMaterial",
            Assert.Throws<GameRuleException>(() => CycleEngine.StartJob(citizen, "9,11", 0, "bring-sticks", context, now)).Code);

        // Labor opens in whole units: a third of the sticks is not enough for one of the two stacking works, half is.
        village.Store(ResourceKind.Wood, 1);
        Job(citizen, "9,11", 0, "bring-sticks", context, ref now);
        Assert.Equal(1, site.LaborRoom(0, "stack-pile"));
    }

    [Fact]
    public void NewTribe_BuildsItsFirstHeap_FromSticksInHand()
    {
        var village = Village.Create("v", "V", Guid.NewGuid(), Now);
        village.Trees.Clear();
        var citizen = NewCitizen(village);
        citizen.Carry(ResourceKind.Wood, 10);
        ConstructionRules.Place(village, BuildingKind.Heap, 9, 11, Now);
        var context = Context(village, 1);
        var now = Now;

        Job(citizen, "9,11", 0, "clear-ground", context, ref now);
        Job(citizen, "9,11", 0, "clear-ground", context, ref now);
        while (village.Sites.Count > 0)
        {
            Job(citizen, "9,11", 0, "bring-sticks", context, ref now);
        }

        Assert.Contains(village.Storages, s => s.Kind == StorageKind.Heap && s.PlotId == "9,11");
        Assert.Equal(6, citizen.Carried(ResourceKind.Wood));
    }

    [Fact]
    public void Haul_ForALaterStage_IsAllowed_LaborIsNot()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Reeds, 20);
        var citizen = NewCitizen(village);
        ConstructionRules.Place(village, BuildingKind.LeanTo, 9, 11, Now);
        var context = Context(village, 1);
        var now = Now;

        Assert.Equal("site.stageNotReady",
            Assert.Throws<GameRuleException>(() => CycleEngine.StartJob(citizen, "9,11", 1, "lay-cover", context, now)).Code);

        Job(citizen, "9,11", 1, "bring-reeds", context, ref now);
        Assert.Equal(5, village.Sites.Single().Done(1, "bring-reeds"));
    }

    [Fact]
    public void Stage_NeedsACrewOfDifferentCitizens()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Wood, 20);
        var first = NewCitizen(village);
        var second = NewCitizen(village);
        ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);
        var context = Context(village, 2);
        var now = Now;

        BuildWoodPileAlone(first, context, ref now);

        var site = village.Sites.Single();
        Assert.True(site.JobsDone(0));
        Assert.Equal(0, site.Stage);
        Assert.Equal("site.alreadyHelped",
            Assert.Throws<GameRuleException>(() =>
                CycleEngine.StartJob(first, "9,11", 0, BuildingCatalog.LendAHand, context, now)).Code);

        var report = Job(second, "9,11", 0, BuildingCatalog.LendAHand, context, ref now);

        Assert.Equal(BuildingKind.WoodPile, report.BuildingCompleted);
        Assert.Empty(village.Sites);
        Assert.Contains(village.Storages, s => s.Kind == StorageKind.WoodPile && s.PlotId == "9,11");
    }

    [Fact]
    public void Crew_ShrinksToTheCitizensWhoArePlaying()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Wood, 20);
        var citizen = NewCitizen(village);
        ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);
        var context = Context(village, 1);
        var now = Now;

        var report = BuildWoodPileAlone(citizen, context, ref now);

        Assert.Equal(BuildingKind.WoodPile, report.BuildingCompleted);
    }

    private static CycleReport BuildWoodPileAlone(Citizen citizen, WorkContext context, ref DateTimeOffset now)
    {
        CycleReport report = null!;
        foreach (var jobId in new[] { "clear-ground", "bring-sticks", "bring-sticks", "stack-pile", "stack-pile" })
        {
            report = Job(citizen, "9,11", 0, jobId, context, ref now);
        }

        return report;
    }

    [Fact]
    public void FinishedLeanTo_GivesBedsToTheFirstTwoCitizens()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Wood, 20);
        village.Store(ResourceKind.Reeds, 20);
        var citizens = Enumerable.Range(0, 3).Select(i => NewCitizen(village, $"C{i}", Now.AddMinutes(i))).ToList();
        ConstructionRules.Place(village, BuildingKind.LeanTo, 9, 11, Now);
        var context = Context(village, 1);
        var now = Now;
        var builder = citizens[0];

        foreach (var (stage, jobId) in new[]
                 {
                     (0, "clear-ground"), (0, "bring-sticks"), (0, "bring-sticks"), (0, "lean-poles"), (0, "lean-poles"),
                     (1, "bring-reeds"), (1, "bring-reeds"), (1, "lay-cover"), (1, "lay-cover"),
                 })
        {
            Job(builder, "9,11", stage, jobId, context, ref now);
        }

        Assert.Contains(village.Structures, s => s.Kind == BuildingKind.LeanTo);
        ShelterRules.Assign(village, citizens, now);
        Assert.Equal([0.9, 0.9, GameRules.UnderTreesRegeneration], citizens.Select(c => c.ShelterQuality));
    }

    [Fact]
    public void Repeats_NeedMoreMaterial()
    {
        var village = NewVillage();
        village.Storages.Add(StorageBuilding.Create(StorageKind.WoodPile, "25,5", Now));

        var site = ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);

        Assert.Equal(Math.Ceiling(6 * BuildingCatalog.RepeatCostGrowth), site.Required(0, "bring-sticks"));
        Assert.Equal(2, site.Required(0, "stack-pile"));
    }

    [Fact]
    public void Cancel_OnlyUntouchedBlueprints()
    {
        var village = NewVillage();
        village.Store(ResourceKind.Wood, 20);
        var citizen = NewCitizen(village);
        ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);
        ConstructionRules.Place(village, BuildingKind.StonePile, 21, 12, Now);
        var now = Now;

        Job(citizen, "9,11", 0, "bring-sticks", Context(village, 1), ref now);

        Assert.Equal("building.cannotCancel",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Cancel(village, "9,11")).Code);
        ConstructionRules.Cancel(village, "21,12");
        Assert.Single(village.Sites);
    }
}
