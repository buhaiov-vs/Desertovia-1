using System.Net;
using System.Security.Cryptography;
using System.Text;
using Desertod.Api.Auth;
using Microsoft.Extensions.Options;

namespace Desertod.Api.Tests;

public sealed class TelegramMiniAppVerifierTests
{
    private const string Token = "123:isolated-test";
    private readonly TestClock clock = new();
    private TelegramMiniAppVerifier Verifier() => new(Options.Create(new TelegramOptions { BotToken = Token }), clock);
    private string Signed(long? at = null, string user = "{\"id\":1001,\"username\":\"tester\"}", string token = Token)
    {
        var fields = new SortedDictionary<string,string>(StringComparer.Ordinal) {
            ["auth_date"] = (at ?? clock.GetUtcNow().ToUnixTimeSeconds()).ToString(), ["query_id"] = "isolated", ["user"] = user };
        var key = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(token));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(string.Join('\n',fields.Select(f=>$"{f.Key}={f.Value}")))));
        return string.Join('&', fields.Select(f=>$"{f.Key}={WebUtility.UrlEncode(f.Value)}")) + "&hash=" + hash;
    }
    [Fact] public void Signed_identity_is_accepted() => Assert.Equal(new TelegramUser("1001", "tester"), Verifier().Verify(Signed()));
    [Fact] public void Forged_token_is_rejected() => Assert.Null(Verifier().Verify(Signed(token:"other:token")));
    [Fact] public void Changed_identity_is_rejected() => Assert.Null(Verifier().Verify(Signed().Replace("1001","1002")));
    [Fact] public void Duplicate_field_is_rejected() => Assert.Null(Verifier().Verify(Signed()+"&auth_date=1"));
    [Fact] public void Old_data_is_rejected() => Assert.Null(Verifier().Verify(Signed(clock.GetUtcNow().ToUnixTimeSeconds()-301)));
    [Fact] public void Future_data_is_rejected() => Assert.Null(Verifier().Verify(Signed(clock.GetUtcNow().ToUnixTimeSeconds()+31)));
    [Theory] [InlineData("{}")][InlineData("{\"id\":-1}")][InlineData("{\"id\":\"1001\"}")][InlineData("[]")]
    public void Invalid_signed_user_is_rejected(string user) => Assert.Null(Verifier().Verify(Signed(user:user)));
}
