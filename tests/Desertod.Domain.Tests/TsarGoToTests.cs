using Desertod.Domain.Rules;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class TsarGoToTests
{
    [Fact]
    public void Tsar_StandsAtTheCampfire_UntilSentSomewhere()
    {
        var village = NewVillage();
        Assert.Null(village.TsarSpot);

        village.TsarGoTo(new TilePoint(3.5, 4.25), Now);

        Assert.Equal(new TilePoint(3.5, 4.25), village.TsarSpot);
    }

    [Fact]
    public void SpotBeyondTheLand_IsRejected()
    {
        var village = NewVillage();
        var land = WorldMap.Land;

        var error = Assert.Throws<GameRuleException>(() => village.TsarGoTo(new TilePoint(land.X + land.Width + 1, land.Y), Now));

        Assert.Equal("spot.outsideLand", error.Code);
        Assert.Null(village.TsarSpot);
    }
}
