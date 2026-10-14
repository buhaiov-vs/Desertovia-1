using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

/// <summary>A signed-in player with a character.</summary>
public sealed record Player(HttpClient Client, string CharacterName);

/// <summary>A citizen of <see cref="Village"/>.</summary>
public sealed record Resident(HttpClient Client, Guid Id, string Path);

/// <summary>A village and the player who founded it.</summary>
public sealed record FoundedVillage(HttpClient Tsar, string Slug, string? InviteCode)
{
    public string Path => $"/api/villages/{Slug}";
}

/// <summary>
/// Players, villages and citizens for use-case tests. What the game takes hours of play to reach (goods in storage,
/// wounds, discoveries) is set up directly in the database.
/// </summary>
public sealed class Players(DesertodApiFactory factory)
{
    public static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    public async Task<HttpClient> SignInAsync(string prefix)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/dev-login", new { name = UniqueName(prefix) });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        return client;
    }

    public async Task<Player> WithCharacterAsync(string characterName)
    {
        var client = await SignInAsync(characterName.ToLowerInvariant());
        var appearance = new { gender = "Female", style = 0, skinTone = 0, hairColor = 0, outfitColor = 0 };
        var created = await client.PostAsJsonAsync("/api/me/character", new { name = characterName, appearance });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return new Player(client, characterName);
    }

    public async Task<FoundedVillage> FoundVillageAsync(string name = "Test Village", bool isPrivate = false)
    {
        var tsar = await WithCharacterAsync("Tsar");
        var created = await OkJsonAsync(await tsar.Client.PostAsJsonAsync("/api/villages", new { name, isPrivate }));
        var slug = created.GetProperty("slug").GetString()!;
        var village = await tsar.Client.GetFromJsonAsync<JsonElement>($"/api/villages/{slug}");
        return new FoundedVillage(tsar.Client, slug, village.GetProperty("inviteCode").GetString());
    }

    public async Task<Resident> JoinAsync(FoundedVillage village, string characterName = "Worker")
    {
        var player = await WithCharacterAsync(characterName);
        var joined = await OkJsonAsync(await player.Client.PostAsJsonAsync(
            $"{village.Path}/citizens", new { inviteCode = village.InviteCode }));
        return new Resident(player.Client, joined.GetProperty("id").GetGuid(), village.Path);
    }

    /// <summary>Changes the village the way hours of play would, at the game's current time.</summary>
    public async Task ArrangeVillageAsync(string slug, Action<Village, DateTimeOffset> change)
    {
        await using var db = await CreateDbContextAsync();
        change(await db.Villages.SingleAsync(v => v.Slug == slug), factory.Clock.GetUtcNow());
        await db.SaveChangesAsync();
    }

    public async Task ArrangeCitizenAsync(Guid citizenId, Action<Citizen, DateTimeOffset> change)
    {
        await using var db = await CreateDbContextAsync();
        change(await db.Citizens.SingleAsync(c => c.Id == citizenId), factory.Clock.GetUtcNow());
        await db.SaveChangesAsync();
    }

    public async Task<Citizen> LoadCitizenAsync(Guid citizenId)
    {
        await using var db = await CreateDbContextAsync();
        return await db.Citizens.AsNoTracking().SingleAsync(c => c.Id == citizenId);
    }

    private Task<DesertodDbContext> CreateDbContextAsync() =>
        factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    public static async Task<JsonElement> OkJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 OK, got {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>The game refused the request for the reason the client shows to the player.</summary>
    public static async Task AssertRejectedAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {(int)status}, got {(int)response.StatusCode}: {body}");
        Assert.Equal(code, JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
    }

    public static Task AssertRuleBrokenAsync(HttpResponseMessage response, string code) =>
        AssertRejectedAsync(response, HttpStatusCode.BadRequest, code);

    public static Task AssertForbiddenAsync(HttpResponseMessage response) =>
        AssertRejectedAsync(response, HttpStatusCode.Forbidden, "access.denied");

    /// <summary>Waits for a background worker; they poll every few seconds of real time.</summary>
    public static async Task EventuallyAsync(Func<Task<bool>> condition, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting until {what}.");
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }
    }
}
