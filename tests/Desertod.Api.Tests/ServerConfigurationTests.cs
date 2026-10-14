using System.Net;
using System.Xml.Linq;
using Desertod.Api.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace Desertod.Api.Tests;

/// <summary>Use case: the operator deploys the server; sign-ins survive restarts, and bad settings stop it at startup.</summary>
public sealed class ServerConfigurationTests
{
    private static readonly string Key = Convert.ToBase64String(Enumerable.Range(0, 64).Select(i => (byte)i).ToArray());

    private static ServiceProvider Server(string? key, string environment = "Production")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataProtection:Key"] = key })
            .Build();
        var services = new ServiceCollection().AddLogging();
        services.AddDesertodDataProtection(configuration, new HostingEnvironment { EnvironmentName = environment });
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Sign_in_cookies_survive_a_restart_with_the_same_key()
    {
        using var before = Server(Key);
        using var after = Server(Key);

        var cookie = before.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie").Protect("signed in");

        Assert.Equal("signed in", after.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie").Unprotect(cookie));
    }

    [Fact]
    public void Production_refuses_to_start_without_a_key()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Server(null));

        Assert.Contains("DataProtection:Key", error.Message);
    }

    [Fact]
    public void Development_starts_without_a_key()
    {
        using var server = Server(null, Environments.Development);

        Assert.NotNull(server.GetRequiredService<IDataProtectionProvider>());
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("c2hvcnQ=")]
    public void Malformed_or_short_key_stops_the_start(string key)
    {
        Assert.Throws<InvalidOperationException>(() => Server(key));
    }

    [Fact]
    public void Configured_key_keeps_its_id_and_cannot_be_replaced_at_runtime()
    {
        var masterKey = Convert.FromBase64String(Key);
        var first = new ConfiguredKeyRepository(masterKey);
        var second = new ConfiguredKeyRepository(masterKey);

        var id = Assert.Single(first.GetAllElements()).Attribute("id")!.Value;

        Assert.Equal(id, Assert.Single(second.GetAllElements()).Attribute("id")!.Value);
        Assert.Throws<InvalidOperationException>(() => first.StoreElement(new XElement("key"), "rotated"));
        Assert.Throws<ArgumentException>(() => new ConfiguredKeyRepository(new byte[ConfiguredKeyRepository.MinimumKeyBytes - 1]));
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    public void Visitors_are_told_apart_by_address_or_household_network(string remote, string expected)
    {
        var http = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse(remote) } };

        Assert.Equal(expected, ClientAddress.Of(http));
    }

    [Fact]
    public void Visitor_without_an_address_has_none()
    {
        Assert.Null(ClientAddress.Of(new DefaultHttpContext()));
    }
}
