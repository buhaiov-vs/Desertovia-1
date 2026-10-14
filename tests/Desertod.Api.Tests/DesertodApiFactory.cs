using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

/// <summary>
/// The real application on a throwaway PostgreSQL container. Runs as Development, so migrations apply, the demo
/// village is seeded and the name-only login is available. Needs Docker.
/// </summary>
public sealed class DesertodApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Signs Telegram logins in tests; without a bot user name the login page does not offer Telegram.</summary>
    public const string TelegramBotToken = "123456:test-bot-token";

    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17").Build();

    /// <summary>The game's clock; see <see cref="TestClock"/>.</summary>
    public TestClock Clock { get; } = new();

    public async Task InitializeAsync() => await database.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Desertod", database.GetConnectionString());
        builder.UseSetting("Authentication:Twitch:ClientId", "");
        builder.UseSetting("Authentication:Google:ClientId", "");
        builder.UseSetting("Authentication:Telegram:BotToken", TelegramBotToken);
        builder.UseSetting("Authentication:Password:AccountsPerAddress", "3");
        // Tests move the clock forward; beasts must not strike the test villages while they do.
        builder.UseSetting("Game:PeacefulTime", "7.00:00:00");
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, TestAddressStartupFilter>());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    /// <summary>The test server has no client address; tests pick one with <see cref="AddressHeader"/>.</summary>
    public const string AddressHeader = "X-Test-Address";

    private sealed class TestAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((http, nextMiddleware) =>
            {
                if (IPAddress.TryParse(http.Request.Headers[AddressHeader], out var address))
                {
                    http.Connection.RemoteIpAddress = address;
                }

                return nextMiddleware(http);
            });
            next(app);
        };
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await database.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<DesertodApiFactory>
{
    public const string Name = "Api";
}
