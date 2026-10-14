using Desertod.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using static Desertod.Api.Tests.TelegramLogin;

namespace Desertod.Api.Tests;

/// <summary>Use case: a player signs in with Telegram; only data Telegram signed for this bot, and recently, is trusted.</summary>
public sealed class TelegramLoginVerifierTests
{
    private const string BotToken = "42:verifier-token";

    private readonly TestClock clock = new();

    private TelegramLoginVerifier Verifier(string? botToken = BotToken) =>
        new(Options.Create(new TelegramOptions { BotToken = botToken }), clock, NullLogger<TelegramLoginVerifier>.Instance);

    private static QueryCollection Query(IReadOnlyDictionary<string, string> fields) =>
        new(fields.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value)));

    private DateTimeOffset Now => clock.GetUtcNow();

    [Fact]
    public void Signed_login_names_the_player_by_username()
    {
        var user = Verifier().Verify(Query(Signed(BotToken, Fields("1001", Now, username: "boyar", firstName: "Ivan"))));

        Assert.Equal(new TelegramUser("1001", "boyar"), user);
    }

    [Fact]
    public void Without_a_username_the_full_name_is_used_then_the_id()
    {
        Assert.Equal("Ivan Grozny", Verifier().Verify(Query(Signed(BotToken, Fields("7", Now, firstName: "Ivan", lastName: "Grozny"))))!.DisplayName);
        Assert.Equal("telegram-8", Verifier().Verify(Query(Signed(BotToken, Fields("8", Now))))!.DisplayName);
    }

    [Fact]
    public void Return_address_is_not_part_of_the_signature()
    {
        var fields = Signed(BotToken, Fields("9", Now, username: "returning"));
        fields[TelegramLoginVerifier.ReturnUrlParameter] = "/v/demo";

        Assert.NotNull(Verifier().Verify(Query(fields)));
    }

    [Fact]
    public void Login_signed_for_another_bot_is_rejected()
    {
        var fields = Signed("99:someone-else", Fields("1001", Now, username: "boyar"));

        Assert.Null(Verifier().Verify(Query(fields)));
    }

    [Fact]
    public void Changed_field_breaks_the_signature()
    {
        var fields = Signed(BotToken, Fields("1001", Now, username: "boyar"));
        fields["username"] = "tsar";

        Assert.Null(Verifier().Verify(Query(fields)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Missing_or_malformed_hash_is_rejected(string hash)
    {
        var fields = Fields("1001", Now, username: "boyar");
        fields["hash"] = hash;

        Assert.Null(Verifier().Verify(Query(fields)));
    }

    [Fact]
    public void Login_older_than_a_day_is_rejected()
    {
        var fields = Signed(BotToken, Fields("1001", Now - TimeSpan.FromDays(1) - TimeSpan.FromMinutes(1), username: "boyar"));

        Assert.Null(Verifier().Verify(Query(fields)));
    }

    [Fact]
    public void Login_from_the_future_is_rejected_beyond_clock_skew()
    {
        Assert.NotNull(Verifier().Verify(Query(Signed(BotToken, Fields("1001", Now + TimeSpan.FromMinutes(4), username: "boyar")))));
        Assert.Null(Verifier().Verify(Query(Signed(BotToken, Fields("1001", Now + TimeSpan.FromMinutes(10), username: "boyar")))));
    }

    [Fact]
    public void Login_without_a_date_or_an_id_is_rejected()
    {
        var undated = Fields("1001", Now, username: "boyar");
        undated.Remove("auth_date");
        var anonymous = Fields("1001", Now, username: "boyar");
        anonymous["id"] = "";

        Assert.Null(Verifier().Verify(Query(Signed(BotToken, undated))));
        Assert.Null(Verifier().Verify(Query(Signed(BotToken, anonymous))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Without_a_bot_token_nobody_signs_in(string? botToken)
    {
        var fields = Signed(BotToken, Fields("1001", Now, username: "boyar"));

        Assert.Null(Verifier(botToken).Verify(Query(fields)));
    }
}
