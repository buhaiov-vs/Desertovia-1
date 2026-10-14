using Desertod.Api.Game;
using Desertod.Domain.Rules;

namespace Desertod.Api.Tests;

/// <summary>Use case: a citizen closes the village page and goes away a few minutes later, then comes back.</summary>
public sealed class CitizenPresenceTests
{
    private static readonly TimeSpan PastWindow = GameRules.PresenceWindow + TimeSpan.FromSeconds(1);

    private readonly TestClock clock = new();
    private readonly Guid citizenId = Guid.NewGuid();

    [Fact]
    public void Open_page_keeps_the_citizen_present_and_closing_it_starts_the_window()
    {
        var presence = new CitizenPresence(clock);
        Assert.True(presence.Connect(citizenId, "village", "connection"));

        clock.Advance(PastWindow);
        Assert.True(presence.IsPresent(citizenId));
        Assert.Empty(presence.TakeDeparted());

        presence.Disconnect("connection");
        clock.Advance(GameRules.PresenceWindow - TimeSpan.FromSeconds(1));
        Assert.True(presence.IsPresent(citizenId));

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(presence.IsPresent(citizenId));
        Assert.Equal([("village", citizenId)], presence.TakeDeparted());
        Assert.Empty(presence.TakeDeparted());
    }

    [Fact]
    public void Acting_brings_the_citizen_back()
    {
        var presence = new CitizenPresence(clock);
        presence.Touch(citizenId, "village");
        Assert.False(presence.Touch(citizenId, "village"));

        clock.Advance(PastWindow);
        presence.TakeDeparted();

        Assert.True(presence.Touch(citizenId, "village"));
        Assert.True(presence.IsPresent(citizenId));
    }

    [Fact]
    public void Unknown_citizens_are_away()
    {
        var presence = new CitizenPresence(clock);

        Assert.False(presence.IsPresent(citizenId));
    }
}
