using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Technologies;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class BakingTests
{
    private static void Discover(Village village, string technologyId) =>
        village.Research.Add(new TechnologyProgress { TechnologyId = technologyId, ResearchedAt = Now });

    private static Village BakeryVillage()
    {
        var village = NewVillage();
        Discover(village, TechnologyCatalog.Agriculture);
        Discover(village, TechnologyCatalog.Baking);
        village.Structures.Add(new Structure { Kind = BuildingKind.Bakery, PlotId = "21,12", BuiltAt = Now });
        return village;
    }

    private static Citizen Baker(Village village) =>
        NewCitizen(village, (Stat.Dexterity, 4), (Stat.Endurance, 2), (Stat.Intelligence, 2));

    [Fact]
    public void Bakery_NeedsBakingAndAFarm()
    {
        var village = NewVillage();
        Discover(village, TechnologyCatalog.Agriculture);

        Assert.Equal("building.needsTechnology",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.Bakery, 21, 12, Now)).Code);

        Discover(village, TechnologyCatalog.Baking);

        Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.Bakery, 21, 12, Now));

        village.Structures.Add(new Structure { Kind = BuildingKind.Farm, PlotId = "24,12", BuiltAt = Now });
        Assert.Equal(BuildingKind.Bakery, ConstructionRules.Place(village, BuildingKind.Bakery, 21, 12, Now).Kind);
    }

    [Fact]
    public void BakingBread_TurnsGrainIntoMoreFood()
    {
        var village = BakeryVillage();
        village.Store(ResourceKind.Grain, 2);
        var food = village.Stock(ResourceKind.Food);
        var baker = Baker(village);

        CycleEngine.Start(baker, Task("bake-bread"), WorkContext.For(village), Now);
        var report = Finish(baker, WorkContext.For(village));

        Assert.False(report.Overflowed);
        Assert.Equal(0, village.Stock(ResourceKind.Grain));
        Assert.True(village.Stock(ResourceKind.Food) > food + 2);
    }

    [Fact]
    public void BakingBread_NeedsGrain()
    {
        var village = BakeryVillage();
        var baker = Baker(village);

        var error = Assert.Throws<GameRuleException>(() => CycleEngine.Start(baker, Task("bake-bread"), WorkContext.For(village), Now));

        Assert.Equal("storage.missingInput", error.Code);
    }

    [Fact]
    public void BakingBread_IsOpenToNovices_AndBaking3MakesABaker()
    {
        var village = BakeryVillage();
        village.Store(ResourceKind.Grain, 2);
        var novice = NewCitizen(village);

        CycleEngine.Start(novice, Task("bake-bread"), WorkContext.For(village), Now);

        Assert.Equal("laborer", GameCatalog.TitleFor(novice).Id);
        Assert.Equal("baker", GameCatalog.TitleFor(novice.WithSkill(Skill.Baking, GameCatalog.JourneymanSkill)).Id);
    }
}
