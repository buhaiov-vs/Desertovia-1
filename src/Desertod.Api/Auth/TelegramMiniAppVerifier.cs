using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Desertod.Api.Auth;

/// <summary>Проверяет подписанные initData Mini App; ID из тела клиента не принимается.</summary>
public sealed class TelegramMiniAppVerifier(IOptions<TelegramOptions> options, TimeProvider time)
{
    public TelegramUser? Verify(string? data)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BotToken) || string.IsNullOrEmpty(data) || data.Length > 16384)
            return null;
        var fields = QueryHelpers.ParseQuery(data);
        if (fields.Any(f => f.Value.Count != 1) || !fields.TryGetValue("hash", out var hash) || hash.ToString().Length != 64)
            return null;
        byte[] received;
        try { received = Convert.FromHexString(hash.ToString()); }
        catch (FormatException) { return null; }
        var check = string.Join('\n', fields.Where(f => f.Key != "hash").OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => $"{f.Key}={f.Value}"));
        var key = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(options.Value.BotToken));
        if (!CryptographicOperations.FixedTimeEquals(received, HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(check))))
            return null;
        if (!fields.TryGetValue("auth_date", out var date) || !long.TryParse(date, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return null;
        var current = time.GetUtcNow().ToUnixTimeSeconds();
        if (seconds < current - 300 || seconds > current + 30) return null;
        if (!fields.TryGetValue("user", out var raw)) return null;
        try
        {
            using var user = JsonDocument.Parse(raw.ToString(), new JsonDocumentOptions { MaxDepth = 8 });
            var root = user.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var number) || number <= 0)
                return null;
            string Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
            var name = Text("username");
            if (string.IsNullOrWhiteSpace(name)) name = (Text("first_name") + " " + Text("last_name")).Trim();
            if (string.IsNullOrWhiteSpace(name)) name = "telegram-" + number;
            return new TelegramUser(number.ToString(CultureInfo.InvariantCulture), name);
        }
        catch (JsonException) { return null; }
    }
}
