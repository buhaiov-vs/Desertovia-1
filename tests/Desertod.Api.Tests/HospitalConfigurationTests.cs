using Desertod.Api.Game;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class HospitalConfigurationTests(DesertodApiFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Application_configuration_controls_the_real_hospital_bridge(bool enabled)
    {
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Game:HospitalEnabled", enabled.ToString());
            builder.UseSetting("Authentication:Telegram:BotUsername", "test_bot");
            builder.UseSetting("Authentication:Telegram:BrowserBridgeUrl", "http://127.0.0.1:8686/dezertovia/auth-bridge");
        });

        // The ordinary API fixture uses a fake bridge. Resolve the real adapter with
        // the application's options, without sending any HTTP or Telegram requests.
        Assert.Equal(enabled, configured.Services.GetRequiredService<IOptions<GameOptions>>().Value.HospitalEnabled);
        var bridge = ActivatorUtilities.CreateInstance<HospitalBridge>(configured.Services);
        Assert.Equal(enabled, bridge.Enabled);
    }
}
