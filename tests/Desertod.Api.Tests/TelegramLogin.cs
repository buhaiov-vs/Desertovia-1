using System.Security.Cryptography;
using System.Text;

namespace Desertod.Api.Tests;

/// <summary>Builds what the Telegram Login Widget sends back, signed the way Telegram signs it.</summary>
public static class TelegramLogin
{
    public static Dictionary<string, string> Signed(string botToken, IReadOnlyDictionary<string, string> fields)
    {
        var dataCheckString = string.Join('\n', fields.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString)));
        return new Dictionary<string, string>(fields) { ["hash"] = hash };
    }

    public static Dictionary<string, string> Fields(string id, DateTimeOffset authDate, string? username = null, string? firstName = null, string? lastName = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = id,
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(),
        };
        if (username is not null)
        {
            fields["username"] = username;
        }

        if (firstName is not null)
        {
            fields["first_name"] = firstName;
        }

        if (lastName is not null)
        {
            fields["last_name"] = lastName;
        }

        return fields;
    }

    public static string QueryString(IReadOnlyDictionary<string, string> fields) =>
        string.Join('&', fields.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
}
