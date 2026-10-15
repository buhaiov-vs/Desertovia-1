namespace Desertod.Domain.Chat;

/// <summary>NPC speech has no account, citizen, role or wallet. Separate storage keeps rollback compatible.</summary>
public sealed class ChaplainMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public required string Text { get; set; }
    public string? Topic { get; set; }
    public DateTimeOffset SentAt { get; set; }

    public static ChaplainMessage Speak(Guid villageId, string text, DateTimeOffset now)
    {
        text = text.Trim();
        if (text.Length is < 1 or > ChatMessage.TextMaximumLength)
            throw new ArgumentException("Chaplain speech must fit a chat line.", nameof(text));
        return new() { VillageId = villageId, Text = text, SentAt = now };
    }
}
