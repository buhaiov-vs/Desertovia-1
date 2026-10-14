using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

/// <summary>Use case: a player settles in a village as a citizen.</summary>
[Collection(ApiCollection.Name)]
public sealed class JoiningVillageTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    [Fact]
    public async Task Private_village_asks_for_the_invite_code()
    {
        var village = await players.FoundVillageAsync("Closed Village", isPrivate: true);
        var newcomer = await players.WithCharacterAsync("Newcomer");
        var path = $"{village.Path}/citizens";

        await AssertRuleBrokenAsync(await newcomer.Client.PostAsJsonAsync(path, new { }), "village.inviteRequired");
        await AssertRuleBrokenAsync(await newcomer.Client.PostAsJsonAsync(path, new { inviteCode = "WRONG123" }), "village.inviteInvalid");

        var spokenAloud = $" {village.InviteCode!.ToLowerInvariant()} ";
        var joined = await OkJsonAsync(await newcomer.Client.PostAsJsonAsync(path, new { inviteCode = spokenAloud }));
        Assert.Equal("Newcomer", joined.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Tsar_cannot_live_in_their_own_village()
    {
        var village = await players.FoundVillageAsync("Tsar Home");

        var response = await village.Tsar.PostAsJsonAsync($"{village.Path}/citizens", new { });

        await AssertRuleBrokenAsync(response, "citizen.tsar");
    }

    [Fact]
    public async Task Citizen_cannot_join_the_same_village_twice()
    {
        var village = await players.FoundVillageAsync("Once Village");
        var citizen = await players.JoinAsync(village);

        var again = await citizen.Client.PostAsJsonAsync($"{village.Path}/citizens", new { });

        await AssertRuleBrokenAsync(again, "citizen.alreadyLives");
    }

    [Fact]
    public async Task Unknown_village_cannot_be_joined()
    {
        var newcomer = await players.WithCharacterAsync("Wanderer");

        var response = await newcomer.Client.PostAsJsonAsync("/api/villages/no-such-village/citizens", new { });

        await AssertRejectedAsync(response, HttpStatusCode.NotFound, "village.notFound");
    }

    [Fact]
    public async Task Newcomer_appears_in_the_village_with_a_place_to_sleep()
    {
        var village = await players.FoundVillageAsync("Welcoming Village");

        var citizen = await players.JoinAsync(village, "Settler");

        var seen = await OkJsonAsync(await village.Tsar.GetAsync(village.Path));
        var listed = Assert.Single(seen.GetProperty("citizens").EnumerateArray());
        Assert.Equal(citizen.Id, listed.GetProperty("id").GetGuid());
        Assert.Equal("Idle", listed.GetProperty("state").GetString());
        var me = await OkJsonAsync(await citizen.Client.GetAsync($"{village.Path}/me"));
        Assert.True(me.GetProperty("shelterQuality").GetDouble() > 0);
        Assert.Equal(me.GetProperty("maximumEnergy").GetDouble(), me.GetProperty("energy").GetDouble(), 3);
    }

    [Theory]
    [InlineData("POST", "me/task")]
    [InlineData("DELETE", "me/task")]
    [InlineData("POST", "me/drop")]
    [InlineData("POST", "me/drink")]
    [InlineData("POST", "me/heal")]
    [InlineData("POST", "me/take")]
    [InlineData("POST", "me/goto")]
    [InlineData("POST", "me/job")]
    [InlineData("POST", "me/research")]
    [InlineData("POST", "me/forecast")]
    [InlineData("POST", "me/vote")]
    public async Task Outsiders_cannot_act_as_citizens(string method, string action)
    {
        var village = await players.FoundVillageAsync("Members Only");
        var outsider = await players.WithCharacterAsync("Outsider");
        var body = action switch
        {
            "me/task" => (object)new { taskId = "chop-wood" },
            "me/take" => new { plotId = "17,12", resource = "Wood", amount = 1 },
            "me/goto" => new { spot = new { x = 10, y = 10 } },
            "me/job" => new { plotId = "9,11", stage = 0, jobId = "clear-ground" },
            "me/forecast" => new { rain = true },
            "me/vote" => new { technologyId = "stone-tools" },
            _ => new { },
        };
        using var request = new HttpRequestMessage(new HttpMethod(method), $"{village.Path}/{action}")
        {
            Content = method == "DELETE" ? null : JsonContent.Create(body),
        };

        var response = await outsider.Client.SendAsync(request);

        await AssertRejectedAsync(response, HttpStatusCode.NotFound, "citizen.notInVillage");
    }

    [Fact]
    public async Task Visitor_who_does_not_live_in_the_village_has_no_citizen_page()
    {
        var village = await players.FoundVillageAsync("Watched Village");
        var viewer = await players.SignInAsync("viewer");

        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"{village.Path}/me")).StatusCode);
        var watched = await viewer.GetFromJsonAsync<JsonElement>(village.Path);
        Assert.Equal(village.Slug, watched.GetProperty("state").GetProperty("slug").GetString());
    }
}
