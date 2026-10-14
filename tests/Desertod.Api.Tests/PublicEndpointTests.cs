using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Buildings;
using Desertod.Domain.Storage;
using Desertod.Domain.Weather;

namespace Desertod.Api.Tests;

/// <summary>What a visitor can reach before and right after signing in. The game itself needs an account.</summary>
[Collection(ApiCollection.Name)]
public sealed class PublicEndpointTests(DesertodApiFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    private async Task<HttpClient> SignedInClientAsync()
    {
        var signedIn = factory.CreateClient();
        var login = await signedIn.PostAsJsonAsync("/auth/dev-login", new { name = $"visitor-{Guid.NewGuid():N}"[..24] });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        return signedIn;
    }

    [Fact]
    public async Task Login_providers_offer_the_development_login()
    {
        var providers = await client.GetFromJsonAsync<JsonElement>("/auth/providers");

        Assert.True(providers.GetProperty("developmentLogin").GetBoolean());
    }

    [Fact]
    public async Task Catalog_lists_tasks_roles_and_rules()
    {
        var catalog = await (await SignedInClientAsync()).GetFromJsonAsync<JsonElement>("/api/catalog");

        Assert.NotEmpty(catalog.GetProperty("tasks").EnumerateArray());
        Assert.NotEmpty(catalog.GetProperty("roles").EnumerateArray());
        Assert.Equal(JsonValueKind.Object, catalog.GetProperty("rules").ValueKind);
        Assert.True(catalog.GetProperty("devTools").GetBoolean());
    }

    [Fact]
    public async Task Village_state_tells_day_or_night_and_its_weather()
    {
        var village = await (await SignedInClientAsync()).GetFromJsonAsync<JsonElement>($"/api/villages/{DemoVillage.Slug}");
        var weather = village.GetProperty("state").GetProperty("weather");

        var startedAt = weather.GetProperty("periodStartedAt").GetDateTimeOffset();

        Assert.Contains(weather.GetProperty("kind").GetString(), Enum.GetNames<WeatherKind>());
        Assert.Equal(DayCycle.IsNight(startedAt), weather.GetProperty("night").GetBoolean());
        Assert.Equal(DayCycle.PeriodLength, weather.GetProperty("periodEndsAt").GetDateTimeOffset() - startedAt);
    }

    [Fact]
    public async Task Open_villages_include_the_demo_village()
    {
        var villages = await (await SignedInClientAsync()).GetFromJsonAsync<JsonElement>("/api/villages/public");

        Assert.Contains(villages.EnumerateArray(), village => village.GetProperty("slug").GetString() == DemoVillage.Slug);
    }

    [Fact]
    public async Task Demo_village_state_loads_with_citizens()
    {
        var village = await (await SignedInClientAsync()).GetFromJsonAsync<JsonElement>($"/api/villages/{DemoVillage.Slug}");

        Assert.Equal(DemoVillage.Slug, village.GetProperty("state").GetProperty("slug").GetString());
        Assert.NotEmpty(village.GetProperty("citizens").EnumerateArray());
    }

    [Fact]
    public async Task Built_village_state_loads_with_every_building()
    {
        var village = await (await SignedInClientAsync()).GetFromJsonAsync<JsonElement>($"/api/villages/{BuiltVillage.Slug}");
        var state = village.GetProperty("state");

        var built = state.GetProperty("structures").EnumerateArray()
            .Concat(state.GetProperty("storages").EnumerateArray())
            .Select(building => building.GetProperty("kind").GetString())
            .ToHashSet();
        Assert.All(Enum.GetNames<BuildingKind>(), kind => Assert.Contains(kind, built));
        Assert.All(Enum.GetNames<StorageKind>(), kind => Assert.Contains(kind, built));
        Assert.All(state.GetProperty("explored").GetBytesFromBase64(), tiles => Assert.Equal(byte.MaxValue, tiles));
    }

    [Fact]
    public async Task Unknown_village_is_not_found()
    {
        var response = await (await SignedInClientAsync()).GetAsync("/api/villages/no-such-village");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/catalog")]
    [InlineData("/api/villages")]
    [InlineData("/api/villages/public")]
    [InlineData($"/api/villages/{DemoVillage.Slug}")]
    [InlineData($"/api/villages/{DemoVillage.Slug}/me")]
    public async Task Game_endpoints_need_a_login(string path)
    {
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/new")]
    [InlineData($"/v/{DemoVillage.Slug}")]
    public async Task Client_routes_do_not_fail_on_the_server(string path)
    {
        var response = await client.GetAsync(path);

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"{path} answered {(int)response.StatusCode}.");
    }
}
