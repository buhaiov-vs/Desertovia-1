using System.Net;
using System.Net.Http.Json;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

/// <summary>Use case: a hero who fell in a fight lies silent in the chat until they are back on their feet.</summary>
[Collection(ApiCollection.Name)]
public sealed class FallenChatTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    [Fact]
    public async Task Fallen_citizen_cannot_write_until_the_wound_heals()
    {
        var village = await players.FoundVillageAsync("Silent Fallen");
        var citizen = await players.JoinAsync(village, "Fallen");
        var path = $"{village.Path}/chat";
        await players.ArrangeCitizenAsync(citizen.Id, (c, now) => c.InjuredUntil = now.AddMinutes(10));

        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync(path, new { text = "Help" }), "chat.fallen");

        await players.ArrangeCitizenAsync(citizen.Id, (c, now) => c.InjuredUntil = now.AddMinutes(-1));
        Assert.Equal(HttpStatusCode.OK, (await citizen.Client.PostAsJsonAsync(path, new { text = "I am up" })).StatusCode);
    }

    [Fact]
    public async Task Fallen_Tsar_cannot_write_until_the_wound_heals()
    {
        var village = await players.FoundVillageAsync("Silent Throne");
        var path = $"{village.Path}/chat";
        await players.ArrangeVillageAsync(village.Slug, (v, now) => v.TsarInjuredUntil = now.AddMinutes(10));

        await AssertRuleBrokenAsync(await village.Tsar.PostAsJsonAsync(path, new { text = "Help" }), "chat.fallen");

        await players.ArrangeVillageAsync(village.Slug, (v, now) => v.TsarInjuredUntil = now.AddMinutes(-1));
        Assert.Equal(HttpStatusCode.OK, (await village.Tsar.PostAsJsonAsync(path, new { text = "I am up" })).StatusCode);
    }
}
