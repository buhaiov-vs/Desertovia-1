using Desertod.Api.Game;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;

namespace Desertod.Api.Tests;

public sealed class CitizenWalkDtoTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CitizenOnTheWay_CarriesTheTrip_SoTheMapFollowsTheFogClock()
    {
        var village = Village.Create("v", "V", Guid.NewGuid(), Now);
        var citizen = CharacterCreation.Create(village.Id, Guid.NewGuid(), "Bob", Now);
        var far = new TilePoint(-30, -20);

        citizen.GoTo(far, village, Now);
        var trip = citizen.Trip!;
        var walk = citizen.ToSummaryDto(Now.AddSeconds(1)).Walk;

        Assert.NotNull(walk);
        Assert.Equal(trip.SetOutAt, walk.SetOutAt);
        Assert.Equal(trip.ArrivesAt, walk.ArrivesAt);
        Assert.Equal(far.X, walk.ToX, 3);
        Assert.Equal(far.Y, walk.ToY, 3);
        Assert.Equal(trip.Way?.Count ?? 0, walk.Way?.Count ?? 0);
    }

    [Fact]
    public void CitizenWhoArrived_CarriesNoTrip()
    {
        var village = Village.Create("v", "V", Guid.NewGuid(), Now);
        var citizen = CharacterCreation.Create(village.Id, Guid.NewGuid(), "Bob", Now);

        citizen.GoTo(new TilePoint(-30, -20), village, Now);

        Assert.Null(citizen.ToSummaryDto(citizen.Trip!.ArrivesAt).Walk);
    }
}
