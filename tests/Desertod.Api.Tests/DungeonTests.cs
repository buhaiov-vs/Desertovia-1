using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Storage;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

/// <summary>Use case: the Tsar calls the tribe to the Dead Lands, citizens answer with a class, and the heroes set out.</summary>
[Collection(ApiCollection.Name)]
public sealed class DungeonTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    private static JsonElement Dungeon(JsonElement state) => state.GetProperty("dungeon");

    private static async Task<JsonElement> StateAsync(FoundedVillage village) =>
        (await OkJsonAsync(await village.Tsar.GetAsync(village.Path))).GetProperty("state");

    [Fact]
    public async Task Tsar_opens_the_dead_lands_and_always_comes_as_the_Voivode()
    {
        var village = await players.FoundVillageAsync("Dead Lands Call");

        var opened = await OkJsonAsync(await village.Tsar.PostAsJsonAsync($"{village.Path}/dungeon", new { dungeonId = "dead-lands" }));

        var run = Dungeon(opened);
        Assert.Equal("Gathering", run.GetProperty("phase").GetString());
        var hero = Assert.Single(run.GetProperty("heroes").EnumerateArray());
        Assert.Equal("Voivode", hero.GetProperty("class").GetString());
        Assert.Equal(JsonValueKind.Null, hero.GetProperty("citizenId").ValueKind);
        await AssertRuleBrokenAsync(
            await village.Tsar.PostAsJsonAsync($"{village.Path}/dungeon", new { dungeonId = "dead-lands" }), "dungeon.alreadyOpen");
    }

    [Fact]
    public async Task Citizens_cannot_open_or_call_off_a_dungeon()
    {
        var village = await players.FoundVillageAsync("No Necromancers");
        var citizen = await players.JoinAsync(village);

        await AssertForbiddenAsync(await citizen.Client.PostAsJsonAsync($"{village.Path}/dungeon", new { dungeonId = "dead-lands" }));
        await AssertForbiddenAsync(await citizen.Client.DeleteAsync($"{village.Path}/dungeon"));
    }

    [Fact]
    public async Task Citizen_picks_a_class_from_the_gear_in_storage_and_gives_it_back_on_leaving()
    {
        var village = await players.FoundVillageAsync("Spear Rack");
        var citizen = await players.JoinAsync(village);
        await players.ArrangeVillageAsync(village.Slug, (v, now) =>
        {
            v.Storages.Add(StorageBuilding.Create(StorageKind.Heap, "17,12", now));
            v.Store(ResourceKind.Spear, 1);
        });
        await village.Tsar.PostAsJsonAsync($"{village.Path}/dungeon", new { dungeonId = "dead-lands" });
        var me = $"{village.Path}/me/dungeon";

        var offers = (await OkJsonAsync(await citizen.Client.GetAsync(me))).EnumerateArray()
            .Select(offer => offer.GetProperty("class").GetString())
            .ToList();
        Assert.Contains("Brawler", offers);
        Assert.Contains("Spearman", offers);
        Assert.DoesNotContain("Swordsman", offers);
        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync(me, new { @class = "Swordsman" }), "dungeon.classUnavailable");

        await OkJsonAsync(await citizen.Client.PostAsJsonAsync(me, new { @class = "Spearman" }));

        var armed = await players.LoadCitizenAsync(citizen.Id);
        Assert.Equal(ResourceKind.Spear, armed.Weapon);
        Assert.NotNull(armed.DungeonRunId);
        var heroes = Dungeon(await StateAsync(village)).GetProperty("heroes").EnumerateArray().ToList();
        Assert.Contains(heroes, hero => hero.GetProperty("class").GetString() == "Spearman");

        await OkJsonAsync(await citizen.Client.DeleteAsync(me));

        var home = await players.LoadCitizenAsync(citizen.Id);
        Assert.Null(home.Weapon);
        Assert.Null(home.DungeonRunId);
        Assert.Single(Dungeon(await StateAsync(village)).GetProperty("heroes").EnumerateArray());
        await AssertRuleBrokenAsync(await citizen.Client.DeleteAsync(me), "dungeon.notJoined");
    }

    [Fact]
    public async Task Wounded_citizens_stay_home()
    {
        var village = await players.FoundVillageAsync("Bandaged Village");
        var citizen = await players.JoinAsync(village);
        await players.ArrangeCitizenAsync(citizen.Id, (c, now) => c.InjuredUntil = now.AddHours(1));
        await village.Tsar.PostAsJsonAsync($"{village.Path}/dungeon", new { dungeonId = "dead-lands" });

        var response = await citizen.Client.PostAsJsonAsync($"{village.Path}/me/dungeon", new { @class = "Brawler" });

        await AssertRuleBrokenAsync(response, "dungeon.injured");
    }

    [Fact]
    public async Task Tsar_calls_off_the_gathering_and_the_heroes_stay_home()
    {
        var village = await players.FoundVillageAsync("Called Off");
        var citizen = await players.JoinAsync(village);
        await village.Tsar.PostAsJsonAsync($"{village.Path}/dungeon", new { dungeonId = "dead-lands" });
        await OkJsonAsync(await citizen.Client.PostAsJsonAsync($"{village.Path}/me/dungeon", new { @class = "Brawler" }));

        var cancelled = await OkJsonAsync(await village.Tsar.DeleteAsync($"{village.Path}/dungeon"));

        Assert.Equal("Cancelled", Dungeon(cancelled).GetProperty("outcome").GetString());
        Assert.Null((await players.LoadCitizenAsync(citizen.Id)).DungeonRunId);
        await AssertRuleBrokenAsync(await village.Tsar.DeleteAsync($"{village.Path}/dungeon"), "dungeon.notGathering");
        await AssertRuleBrokenAsync(
            await citizen.Client.PostAsJsonAsync($"{village.Path}/me/dungeon", new { @class = "Brawler" }), "dungeon.notGathering");
    }

    [Fact]
    public async Task Heroes_march_to_the_edge_of_the_land_and_the_fight_starts()
    {
        var village = await players.FoundVillageAsync("Marching Village");
        var citizen = await players.JoinAsync(village);
        await village.Tsar.PostAsJsonAsync($"{village.Path}/dungeon", new { dungeonId = "dead-lands" });
        await OkJsonAsync(await citizen.Client.PostAsJsonAsync($"{village.Path}/me/dungeon", new { @class = "Brawler" }));

        factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await EventuallyAsync(
            async () => Dungeon(await StateAsync(village)).GetProperty("phase").GetString() != "Gathering",
            "the heroes set out");

        var marching = Dungeon(await StateAsync(village));
        Assert.All(marching.GetProperty("heroes").EnumerateArray(),
            hero => Assert.NotEqual(JsonValueKind.Null, hero.GetProperty("arrivesAt").ValueKind));
        await AssertRuleBrokenAsync(
            await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/goto", new { spot = new { x = 10, y = 10 } }), "dungeon.busy");

        factory.Clock.Advance(TimeSpan.FromSeconds(46));
        await EventuallyAsync(
            async () => Dungeon(await StateAsync(village)).GetProperty("phase").GetString() == "Running",
            "the fight starts");
    }
}
