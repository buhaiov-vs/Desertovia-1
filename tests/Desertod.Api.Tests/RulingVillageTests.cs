using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Entities;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

/// <summary>Use case: the Tsar rules the village: names citizens, plans buildings and calls the undead for the viewers.</summary>
[Collection(ApiCollection.Name)]
public sealed class RulingVillageTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    [Fact]
    public async Task Tsar_writes_and_clears_a_subtitle_under_a_citizen()
    {
        var village = await players.FoundVillageAsync("Titled Village");
        var citizen = await players.JoinAsync(village, "Hero");
        var path = $"{village.Path}/citizens/{citizen.Id}/subtitle";

        var titled = await OkJsonAsync(await village.Tsar.PutAsJsonAsync(path, new { subtitle = "  Slayer of wolves " }));
        Assert.Equal("Slayer of wolves", titled.GetProperty("subtitle").GetString());

        var listed = await OkJsonAsync(await citizen.Client.GetAsync(village.Path));
        Assert.Equal("Slayer of wolves", listed.GetProperty("citizens")[0].GetProperty("subtitle").GetString());

        var cleared = await OkJsonAsync(await village.Tsar.PutAsJsonAsync(path, new { subtitle = "   " }));
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("subtitle").ValueKind);
    }

    [Fact]
    public async Task Subtitle_has_a_length_limit()
    {
        var village = await players.FoundVillageAsync("Long Titles");
        var citizen = await players.JoinAsync(village);

        var response = await village.Tsar.PutAsJsonAsync(
            $"{village.Path}/citizens/{citizen.Id}/subtitle", new { subtitle = new string('x', Citizen.SubtitleMaximumLength + 1) });

        await AssertRuleBrokenAsync(response, "citizen.subtitleLength");
    }

    [Fact]
    public async Task Only_the_Tsar_writes_subtitles()
    {
        var village = await players.FoundVillageAsync("Humble Village");
        var citizen = await players.JoinAsync(village);

        var response = await citizen.Client.PutAsJsonAsync($"{village.Path}/citizens/{citizen.Id}/subtitle", new { subtitle = "Self-made" });

        await AssertForbiddenAsync(response);
    }

    [Fact]
    public async Task Tsar_cannot_name_a_citizen_of_another_village()
    {
        var village = await players.FoundVillageAsync("First Realm");
        var other = await players.FoundVillageAsync("Second Realm");
        var foreigner = await players.JoinAsync(other);

        var response = await village.Tsar.PutAsJsonAsync($"{village.Path}/citizens/{foreigner.Id}/subtitle", new { subtitle = "Spy" });

        await AssertRejectedAsync(response, HttpStatusCode.NotFound, "citizen.notFound");
    }

    [Fact]
    public async Task Tsar_cannot_cancel_a_blueprint_that_is_not_there()
    {
        var village = await players.FoundVillageAsync("Empty Plots");

        var response = await village.Tsar.DeleteAsync($"{village.Path}/blueprints/9,11");

        await AssertRuleBrokenAsync(response, "site.notFound");
    }

    [Fact]
    public async Task Only_the_Tsar_walks_as_the_Tsar()
    {
        var village = await players.FoundVillageAsync("Tsar Walk");
        var citizen = await players.JoinAsync(village);

        var response = await citizen.Client.PostAsJsonAsync($"{village.Path}/tsar/goto", new { spot = new { x = 10, y = 10 } });

        await AssertForbiddenAsync(response);
    }

    [Fact]
    public async Task Tsar_calls_the_undead_and_sends_them_back()
    {
        var village = await players.FoundVillageAsync("Haunted Village");
        await players.JoinAsync(village);
        var undead = $"{village.Path}/undead";

        var called = await OkJsonAsync(await village.Tsar.PostAsJsonAsync(undead, new { enemy = "Skeleton", size = "Medium" }));

        var raids = called.GetProperty("raids").EnumerateArray().ToList();
        Assert.Equal(2, raids.Count);
        Assert.All(raids, raid =>
        {
            Assert.Equal("Undead", raid.GetProperty("source").GetString());
            Assert.Equal("Skeleton", raid.GetProperty("enemy").GetString());
            Assert.Equal("Pending", raid.GetProperty("outcome").GetString());
        });
        await AssertRuleBrokenAsync(
            await village.Tsar.PostAsJsonAsync(undead, new { enemy = "Zombie", size = "Small" }), "raid.undeadComing");

        var sentBack = await OkJsonAsync(await village.Tsar.DeleteAsync(undead));

        Assert.Empty(sentBack.GetProperty("raids").EnumerateArray());
        await AssertRuleBrokenAsync(await village.Tsar.DeleteAsync(undead), "raid.noUndead");
    }

    [Fact]
    public async Task Only_the_undead_answer_the_call()
    {
        var village = await players.FoundVillageAsync("Wolf Call");

        var response = await village.Tsar.PostAsJsonAsync($"{village.Path}/undead", new { enemy = "Wolf", size = "Small" });

        await AssertRuleBrokenAsync(response, "raid.notUndead");
    }

    [Fact]
    public async Task Citizens_cannot_call_or_send_back_the_undead()
    {
        var village = await players.FoundVillageAsync("Necromancy Banned");
        var citizen = await players.JoinAsync(village);

        await AssertForbiddenAsync(await citizen.Client.PostAsJsonAsync($"{village.Path}/undead", new { enemy = "Zombie", size = "Small" }));
        await AssertForbiddenAsync(await citizen.Client.DeleteAsync($"{village.Path}/undead"));
    }
}
