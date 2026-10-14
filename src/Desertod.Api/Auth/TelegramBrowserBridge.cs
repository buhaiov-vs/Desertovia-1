using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Desertod.Api.Auth;

public sealed record BrowserLoginStart(string Raw, string Url, string Pin);
public sealed record BrowserLoginPoll(bool Pending, string? Id, string? Name);

/// <summary>Использует уже существующее подтверждение входа у батюшки, только через доверенный loopback.</summary>
public sealed class TelegramBrowserBridge(HttpClient client, IOptions<TelegramOptions> options)
{
    public bool Enabled => Uri.TryCreate(options.Value.BrowserBridgeUrl, UriKind.Absolute, out var url)
        && url.IsLoopback && url.Scheme == "http" && options.Value.IsConfigured;

    public async Task<T> CallAsync<T>(string action, object body, CancellationToken cancellationToken)
    {
        if (!Enabled || action is not ("start" or "poll")) throw new InvalidOperationException("Browser login is unavailable.");
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.BrowserBridgeUrl!.TrimEnd('/') + "/" + action);
        request.Headers.Add("X-Desertovia-Bridge", Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes("desertovia-bridge:" + options.Value.BotToken))));
        request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw new InvalidOperationException("Invalid login reply.");
    }
}
