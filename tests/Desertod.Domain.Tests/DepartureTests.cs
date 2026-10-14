using Desertod.Domain.Catalog;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class DepartureTests
{
    [Fact]
    public void Leave_DuringTheGathering_DropsOutOfTheRun_AndLeavesGearAndGoodsInStorage()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village).WithSkill(Skill.Guarding, 2);
        village.Store(ResourceKind.Sword, 1);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);
        DungeonRules.Join(run, village, citizen, HeroClass.Swordsman, Now);
        citizen.Carry(ResourceKind.Wood, 3);
        var wood = village.Stock(ResourceKind.Wood);

        Departure.Leave(citizen, village, run, Now);

        Assert.Single(run.Heroes);
        Assert.Null(citizen.DungeonRunId);
        Assert.Null(citizen.Weapon);
        Assert.Equal(1, village.Stock(ResourceKind.Sword));
        Assert.Equal(wood + 3, village.Stock(ResourceKind.Wood));
        Assert.Empty(citizen.Load);
    }

    [Fact]
    public void Leave_AfterTheHeroesSetOut_KeepsTheHeroInTheRun_ButPutsTheGearBack()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village).WithSkill(Skill.Guarding, 2);
        village.Store(ResourceKind.Sword, 1);
        var run = DungeonRules.Open(village, DungeonCatalog.DeadLands, "Tsar", 1, Now);
        DungeonRules.Join(run, village, citizen, HeroClass.Swordsman, Now);
        DungeonRules.March(run, village, [citizen], run.GatheringEndsAt);

        Departure.Leave(citizen, village, run, run.GatheringEndsAt);

        Assert.NotNull(run.Hero(citizen.Id));
        Assert.Null(citizen.DungeonRunId);
        Assert.Equal(1, village.Stock(ResourceKind.Sword));
    }
}
