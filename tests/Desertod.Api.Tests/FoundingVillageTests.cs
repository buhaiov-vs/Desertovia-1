using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

/// <summary>Use case: a player founds a village, names it and decides who may join.</summary>
[Collection(ApiCollection.Name)]
public sealed class FoundingVillageTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    private static async Task<string> SlugOfAsync(HttpClient tsar, string name)
    {
        var created = await OkJsonAsync(await tsar.PostAsJsonAsync("/api/villages", new { name }));
        return created.GetProperty("slug").GetString()!;
    }

    [Fact]
    public async Task Village_address_is_made_from_its_name()
    {
        var tsar = await players.SignInAsync("slug");
        var unique = Guid.NewGuid().ToString("N")[..8];

        Assert.Equal($"river-bend-{unique}", await SlugOfAsync(tsar, $"  River   Bend!! {unique} "));
        Assert.Equal($"zhar-ptitsa-{unique}", await SlugOfAsync(tsar, $"Жар-птица {unique}"));
    }

    [Fact]
    public async Task Villages_with_the_same_name_get_different_addresses()
    {
        var tsar = await players.SignInAsync("twins");
        var name = $"Twin {Guid.NewGuid():N}"[..20];

        var first = await SlugOfAsync(tsar, name);
        var second = await SlugOfAsync(tsar, name);

        Assert.NotEqual(first, second);
        Assert.Equal($"{first}-2", second);
    }

    [Fact]
    public async Task Village_cannot_take_the_address_of_the_public_list()
    {
        var tsar = await players.SignInAsync("public");

        var slug = await SlugOfAsync(tsar, "Public");

        Assert.StartsWith("public-", slug);
        var village = await OkJsonAsync(await tsar.GetAsync($"/api/villages/{slug}"));
        Assert.Equal("Public", village.GetProperty("state").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Name_without_letters_or_a_long_name_still_gets_an_address()
    {
        var tsar = await players.SignInAsync("odd-names");

        Assert.StartsWith("village", await SlugOfAsync(tsar, "!!! ???"));
        var longSlug = await SlugOfAsync(tsar, new string('a', 30) + " " + new string('b', 30));
        Assert.True(longSlug.Length <= 42, longSlug);
        Assert.False(longSlug.EndsWith('-'), longSlug);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("   ")]
    public async Task Village_name_must_be_long_enough(string name)
    {
        var tsar = await players.SignInAsync("short");

        await AssertRuleBrokenAsync(await tsar.PostAsJsonAsync("/api/villages", new { name }), "village.nameLength");
    }

    [Fact]
    public async Task Village_description_has_a_length_limit()
    {
        var tsar = await players.SignInAsync("wordy");

        var response = await tsar.PostAsJsonAsync("/api/villages", new { name = "Wordy Village", description = new string('x', 281) });

        await AssertRuleBrokenAsync(response, "village.descriptionLength");
    }

    [Fact]
    public async Task Private_village_is_hidden_from_the_public_list()
    {
        var open = await players.FoundVillageAsync("Open Village");
        var hidden = await players.FoundVillageAsync("Hidden Village", isPrivate: true);

        var listed = (await open.Tsar.GetFromJsonAsync<JsonElement>("/api/villages/public")).EnumerateArray()
            .Select(v => v.GetProperty("slug").GetString())
            .ToList();

        Assert.Contains(open.Slug, listed);
        Assert.DoesNotContain(hidden.Slug, listed);
        Assert.Null(open.InviteCode);
        Assert.NotNull(hidden.InviteCode);
    }

    [Fact]
    public async Task Players_list_villages_they_rule_and_villages_they_live_in()
    {
        var ruled = await players.FoundVillageAsync("Ruled Village");
        var home = await players.FoundVillageAsync("Home Village");
        var citizen = await players.JoinAsync(home);

        var mine = (await citizen.Client.GetFromJsonAsync<JsonElement>("/api/villages")).EnumerateArray().ToList();

        var lived = Assert.Single(mine);
        Assert.Equal(home.Slug, lived.GetProperty("slug").GetString());
        Assert.False(lived.GetProperty("isOwner").GetBoolean());
        var owned = Assert.Single((await ruled.Tsar.GetFromJsonAsync<JsonElement>("/api/villages")).EnumerateArray());
        Assert.True(owned.GetProperty("isOwner").GetBoolean());
    }

    [Fact]
    public async Task Only_the_Tsar_sees_the_invite_code()
    {
        var village = await players.FoundVillageAsync("Secret Village", isPrivate: true);
        var visitor = await players.SignInAsync("visitor");

        var seen = await OkJsonAsync(await visitor.GetAsync(village.Path));

        Assert.False(seen.GetProperty("isOwner").GetBoolean());
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("inviteCode").ValueKind);
    }

    [Fact]
    public async Task Tsar_replaces_the_invite_code_and_the_old_one_stops_working()
    {
        var village = await players.FoundVillageAsync("Guarded Village", isPrivate: true);

        var renewed = await OkJsonAsync(await village.Tsar.PostAsync($"{village.Path}/invite", null));
        var newCode = renewed.GetProperty("inviteCode").GetString();

        Assert.NotEqual(village.InviteCode, newCode);
        var late = await players.WithCharacterAsync("Late");
        await AssertRuleBrokenAsync(
            await late.Client.PostAsJsonAsync($"{village.Path}/citizens", new { inviteCode = village.InviteCode }),
            "village.inviteInvalid");
        await OkJsonAsync(await late.Client.PostAsJsonAsync($"{village.Path}/citizens", new { inviteCode = newCode }));
    }

    [Fact]
    public async Task Open_village_needs_no_invite_code()
    {
        var village = await players.FoundVillageAsync("Open Gates");

        await AssertRuleBrokenAsync(await village.Tsar.PostAsync($"{village.Path}/invite", null), "village.notPrivate");
    }

    [Fact]
    public async Task Only_the_Tsar_replaces_the_invite_code()
    {
        var village = await players.FoundVillageAsync("Tsar Only", isPrivate: true);
        var stranger = await players.SignInAsync("stranger");

        await AssertForbiddenAsync(await stranger.PostAsync($"{village.Path}/invite", null));
    }

    [Fact]
    public async Task Invite_code_of_an_unknown_village_is_not_found()
    {
        var tsar = await players.SignInAsync("lost");

        var response = await tsar.PostAsync("/api/villages/no-such-village/invite", null);

        await AssertRejectedAsync(response, HttpStatusCode.NotFound, "village.notFound");
    }
}
