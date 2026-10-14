using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static Desertod.Api.Tests.TelegramLogin;

namespace Desertod.Api.Tests;

/// <summary>Use case: a player signs in through another service and lands back where they started.</summary>
[Collection(ApiCollection.Name)]
public sealed class ExternalLoginTests(DesertodApiFactory factory)
{
    private HttpClient NonRedirectingClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string UniqueTelegramId() => Random.Shared.NextInt64(1, long.MaxValue).ToString();

    [Fact]
    public async Task Telegram_login_signs_in_and_returns_to_the_page()
    {
        var client = NonRedirectingClient();
        var username = Players.UniqueName("tg");
        var fields = Signed(DesertodApiFactory.TelegramBotToken, Fields(UniqueTelegramId(), factory.Clock.GetUtcNow(), username: username));
        fields["returnUrl"] = "/v/demo-bridge";

        var callback = await client.GetAsync($"/auth/telegram/callback?{QueryString(fields)}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/v/demo-bridge", callback.Headers.Location?.OriginalString);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal(username, me.GetProperty("displayName").GetString());
        Assert.Contains("telegram", me.GetProperty("providers").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Same_Telegram_user_comes_back_to_the_same_account()
    {
        var id = UniqueTelegramId();
        async Task<Guid> SignInAsync()
        {
            var client = NonRedirectingClient();
            var fields = Signed(DesertodApiFactory.TelegramBotToken, Fields(id, factory.Clock.GetUtcNow(), firstName: "Returning"));
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync($"/auth/telegram/callback?{QueryString(fields)}")).StatusCode);
            return (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("id").GetGuid();
        }

        Assert.Equal(await SignInAsync(), await SignInAsync());
    }

    [Theory]
    [InlineData("//evil.example/steal")]
    [InlineData("/\\evil.example")]
    [InlineData("https://evil.example")]
    public async Task Telegram_login_never_returns_to_another_site(string returnUrl)
    {
        var fields = Signed(DesertodApiFactory.TelegramBotToken, Fields(UniqueTelegramId(), factory.Clock.GetUtcNow(), username: Players.UniqueName("tg")));
        fields["returnUrl"] = returnUrl;

        var callback = await NonRedirectingClient().GetAsync($"/auth/telegram/callback?{QueryString(fields)}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Forged_Telegram_login_is_refused()
    {
        var client = NonRedirectingClient();
        var fields = Signed("1:forged", Fields(UniqueTelegramId(), factory.Clock.GetUtcNow(), username: "impostor"));

        var callback = await client.GetAsync($"/auth/telegram/callback?{QueryString(fields)}");

        Assert.Equal(HttpStatusCode.Unauthorized, callback.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Theory]
    [InlineData("twitch")]
    [InlineData("google")]
    [InlineData("myspace")]
    public async Task Providers_that_are_not_configured_are_not_offered(string provider)
    {
        var response = await NonRedirectingClient().GetAsync($"/auth/login/{provider}?returnUrl=/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Development_login_needs_a_reasonable_name()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/auth/dev-login", new { name = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/auth/dev-login", new { name = new string('n', 33) })).StatusCode);
    }

    [Fact]
    public async Task Health_check_answers_the_container_probe()
    {
        var response = await factory.CreateClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
