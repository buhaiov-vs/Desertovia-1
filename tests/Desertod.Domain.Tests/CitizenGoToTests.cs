using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class CitizenGoToTests
{
    [Fact]
    public void IdleCitizen_StaysWhereSent_UntilTheNextTask()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);

        citizen.GoTo(new TilePoint(3.5, 4.25), village, Now);
        Assert.Equal(new TilePoint(3.5, 4.25), citizen.IdleSpot);

        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);
        Assert.Null(citizen.IdleSpot);
    }

    [Fact]
    public void WorkingCitizen_CannotBeSent()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        CycleEngine.Start(citizen, Task("chop-wood"), WorkContext.For(village), Now);

        var error = Assert.Throws<GameRuleException>(() => citizen.GoTo(new TilePoint(3, 4), village, Now.AddSeconds(10)));

        Assert.Equal("goto.working", error.Code);
        citizen.GoTo(new TilePoint(3, 4), village, citizen.CycleEndsAt!.Value);
    }

    [Fact]
    public void SpotBeyondTheLand_IsRejected()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var land = WorldMap.Land;

        var error = Assert.Throws<GameRuleException>(() => citizen.GoTo(new TilePoint(land.X + land.Width + 1, land.Y), village, Now));

        Assert.Equal("spot.outsideLand", error.Code);
        Assert.Null(citizen.IdleSpot);
    }
}
