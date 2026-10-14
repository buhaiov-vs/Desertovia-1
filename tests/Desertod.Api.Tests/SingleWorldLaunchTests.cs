using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Desertod.Api.Tests;
[Collection(ApiCollection.Name)]
public sealed class SingleWorldLaunchTests(DesertodApiFactory factory)
{
    [Fact]
    public async Task Configured_world_is_reported_and_other_world_creation_is_blocked()
    {
        await using var mounted = factory.WithWebHostBuilder(builder => builder.UseSetting("Hosting:PrimaryVillage", "dezertovia"));
        using var client = mounted.CreateClient();
        var providers = await client.GetFromJsonAsync<JsonElement>("/auth/providers");
        Assert.Equal("dezertovia", providers.GetProperty("primaryVillage").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/villages", new { name = "Other" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/villages/other")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/villages/dezertovia")).StatusCode);
    }
    [Fact]
    public async Task Chaplain_endpoint_requires_a_signed_in_village_member()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/villages/demo-bridge/sramoslav", new { text = "Hello" })).StatusCode);
        await client.PostAsJsonAsync("/auth/dev-login", new { name = "outsider-" + Guid.NewGuid().ToString("N")[..12] });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/villages/demo-bridge/sramoslav", new { text = "Hello" })).StatusCode);
    }
}
