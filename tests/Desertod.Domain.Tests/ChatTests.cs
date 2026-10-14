using Desertod.Domain.Chat;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class ChatTests
{
    private static readonly Guid VillageId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public void Message_is_trimmed_and_signed_by_its_author()
    {
        var citizenId = Guid.NewGuid();

        var message = ChatMessage.Write(VillageId, AccountId, citizenId, "Ann", "  Hello, village!  ", Now);

        Assert.Equal("Hello, village!", message.Text);
        Assert.Equal("Ann", message.AuthorName);
        Assert.Equal(citizenId, message.CitizenId);
        Assert.Equal(Now, message.SentAt);
        Assert.False(message.IsTsar);
    }

    [Fact]
    public void Message_without_a_citizen_is_the_Tsars()
    {
        var message = ChatMessage.Write(VillageId, AccountId, null, "Tsar", "Hear me", Now);

        Assert.True(message.IsTsar);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_message_is_refused(string? text)
    {
        var error = Assert.Throws<GameRuleException>(() => ChatMessage.Write(VillageId, AccountId, null, "Tsar", text, Now));

        Assert.Equal("chat.empty", error.Code);
    }

    [Fact]
    public void Message_fits_a_speech_bubble()
    {
        var longest = new string('a', ChatMessage.TextMaximumLength);

        Assert.Equal(longest, ChatMessage.Write(VillageId, AccountId, null, "Tsar", longest, Now).Text);
        var error = Assert.Throws<GameRuleException>(() => ChatMessage.Write(VillageId, AccountId, null, "Tsar", longest + "a", Now));
        Assert.Equal("chat.length", error.Code);
        Assert.Equal(ChatMessage.TextMaximumLength, error.Arguments["maximum"]);
    }

    [Fact]
    public void Long_author_name_is_cut_to_fit()
    {
        var name = new string('n', ChatMessage.AuthorNameMaximumLength + 10);

        var message = ChatMessage.Write(VillageId, AccountId, null, name, "Hi", Now);

        Assert.Equal(ChatMessage.AuthorNameMaximumLength, message.AuthorName.Length);
    }
}
