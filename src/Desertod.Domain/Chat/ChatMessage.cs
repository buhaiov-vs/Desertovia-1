namespace Desertod.Domain.Chat;

/// <summary>A line a citizen or the Tsar wrote in the village chat.</summary>
public sealed class ChatMessage
{
    /// <summary>Short enough to fit a speech bubble over the author's head.</summary>
    public const int TextMaximumLength = 120;
    public const int AuthorNameMaximumLength = 64;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Null when the Tsar wrote it.</summary>
    public Guid? CitizenId { get; set; }

    /// <summary>The name at the time of writing, so the line reads the same after a rename.</summary>
    public required string AuthorName { get; set; }

    public required string Text { get; set; }
    public DateTimeOffset SentAt { get; set; }

    public bool IsTsar => CitizenId is null;

    public static ChatMessage Write(Guid villageId, Guid accountId, Guid? citizenId, string authorName, string? text, DateTimeOffset now)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new GameRuleException("chat.empty", "Write something first.");
        }

        if (text.Length > TextMaximumLength)
        {
            throw new GameRuleException(
                "chat.length",
                $"A message can be at most {TextMaximumLength} characters long.",
                new Dictionary<string, object> { ["maximum"] = TextMaximumLength });
        }

        return new ChatMessage
        {
            VillageId = villageId,
            AccountId = accountId,
            CitizenId = citizenId,
            AuthorName = authorName.Length > AuthorNameMaximumLength ? authorName[..AuthorNameMaximumLength] : authorName,
            Text = text,
            SentAt = now,
        };
    }
}
