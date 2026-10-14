using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Desertod.Api.Auth;

public sealed class TelegramOptions
{
    public string? BotToken { get; set; }
    public string? BotUsername { get; set; }
    public string? BrowserBridgeUrl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(BotUsername);
}

public sealed record TelegramUser(string Id, string DisplayName);

/// <summary>
/// Verifies data from the Telegram Login Widget:
/// https://core.telegram.org/widgets/login#checking-authorization
/// </summary>
public sealed class TelegramLoginVerifier(IOptions<TelegramOptions> options, TimeProvider time, ILogger<TelegramLoginVerifier> logger)
{
    public const string ReturnUrlParameter = "returnUrl";

    private static readonly TimeSpan MaximumAge = TimeSpan.FromDays(1);
    private static readonly TimeSpan MaximumClockSkew = TimeSpan.FromMinutes(5);

    public TelegramUser? Verify(IQueryCollection query)
    {
        var botToken = options.Value.BotToken;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            return Reject("the bot token is not configured");
        }

        var hash = query["hash"].ToString();
        if (hash.Length != 64)
        {
            return Reject("the hash is missing or malformed");
        }

        var dataCheckString = string.Join('\n', query
            .Where(pair => pair.Key != "hash" && pair.Key != ReturnUrlParameter)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));

        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        var expected = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));

        byte[] received;
        try
        {
            received = Convert.FromHexString(hash);
        }
        catch (FormatException)
        {
            return Reject("the hash is not hexadecimal");
        }

        if (!CryptographicOperations.FixedTimeEquals(expected, received))
        {
            return Reject("the signature does not match");
        }

        if (!long.TryParse(query["auth_date"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var authDate))
        {
            return Reject("auth_date is missing or malformed");
        }

        var age = time.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(authDate);
        if (age > MaximumAge || age < -MaximumClockSkew)
        {
            return Reject($"the login is {age} old");
        }

        var id = query["id"].ToString();
        if (string.IsNullOrEmpty(id))
        {
            return Reject("the user id is missing");
        }

        var username = query["username"].ToString();
        var fullName = $"{query["first_name"]} {query["last_name"]}".Trim();
        var displayName = !string.IsNullOrEmpty(username) ? username : fullName.Length > 0 ? fullName : $"telegram-{id}";

        return new TelegramUser(id, displayName);
    }

    private TelegramUser? Reject(string reason)
    {
        logger.TelegramRejected(reason);
        return null;
    }
}
