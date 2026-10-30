using Desertod.Domain.Rules;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class TaskReachTests
{
    [Fact]
    public void A_spot_close_enough_is_worked()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);

        CycleEngine.Start(citizen, Task("quarry-stone"), WorkContext.For(village), Now, new TilePoint(-5, 10));

        Assert.Equal("quarry-stone", citizen.CurrentTaskId);
    }

    [Fact]
    public void A_spot_whose_way_takes_more_than_half_of_the_work_is_refused_and_nothing_starts()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);

        var refused = Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("quarry-stone"), WorkContext.For(village), Now, new TilePoint(-400, 10)));

        Assert.Equal("task.tooFar", refused.Code);
        Assert.True((int)refused.Arguments["walk"] > (int)refused.Arguments["limit"]);
        Assert.Null(citizen.CurrentTaskId);
    }
}
