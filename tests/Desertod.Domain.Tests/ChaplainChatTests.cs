using Desertod.Domain.Chat;
namespace Desertod.Domain.Tests;
public sealed class ChaplainChatTests
{
    [Fact]
    public void Chaplain_speech_has_no_player_identity_and_fits_the_chat()
    {
        var village = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var line = ChaplainMessage.Speak(village, " Держимся вместе, чада! ", now);
        Assert.Equal(village, line.VillageId);
        Assert.Equal(now, line.SentAt);
        Assert.Equal("Держимся вместе, чада!", line.Text);
        Assert.Throws<ArgumentException>(() => ChaplainMessage.Speak(village, " ", now));
        Assert.Throws<ArgumentException>(() => ChaplainMessage.Speak(village, new string('a',121), now));
    }
}
