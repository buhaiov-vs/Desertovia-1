using Desertod.Domain.Chat;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

/// <param name="CitizenId">Null when the Tsar wrote it.</param>
public sealed record ChatMessageDto(Guid Id, string AuthorName, Guid? CitizenId, bool IsTsar, string Text, DateTimeOffset SentAt);

public sealed record SendChatRequest(string? Text);

/// <summary>Village chat: only the Tsar and the citizens of a village read and write it.</summary>
public sealed class ChatService(
    IDbContextFactory<DesertodDbContext> contextFactory,
    TimeProvider time,
    VillageNotifier notifier,
    ILogger<ChatService> logger)
{
    public const string RateLimit = "chat";

    /// <summary>Messages a player sees on opening the village.</summary>
    public const int HistoryLength = 50;

    private sealed record Member(Guid VillageId, string Slug, Guid? CitizenId, string Name);

    public async Task<bool> IsMemberAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await FindMemberAsync(db, slug, accountId, cancellationToken) is not null;
    }

    /// <summary>The latest messages, oldest first.</summary>
    public async Task<IReadOnlyList<ChatMessageDto>> HistoryAsync(string slug, Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var member = await RequireMemberAsync(db, slug, accountId, cancellationToken);
        var messages = await db.ChatMessages.AsNoTracking()
            .Where(m => m.VillageId == member.VillageId)
            .OrderByDescending(m => m.SentAt)
            .Take(HistoryLength)
            .ToListAsync(cancellationToken);
        return messages.AsEnumerable().Reverse().Select(ToDto).ToList();
    }

    public async Task<ChatMessageDto> SendAsync(string slug, Guid accountId, string? text, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var member = await RequireMemberAsync(db, slug, accountId, cancellationToken);
        var message = ChatMessage.Write(member.VillageId, accountId, member.CitizenId, member.Name, text, time.GetUtcNow());
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        logger.ChatMessageSent(accountId, member.Slug, message.Text.Length);

        var dto = ToDto(message);
        await notifier.ChatMessageSent(member.Slug, dto);
        return dto;
    }

    private static async Task<Member> RequireMemberAsync(DesertodDbContext db, string slug, Guid accountId, CancellationToken cancellationToken)
    {
        if (await FindMemberAsync(db, slug, accountId, cancellationToken) is { } member)
        {
            return member;
        }

        if (!await db.Villages.AnyAsync(v => v.Slug == slug, cancellationToken))
        {
            throw new NotFoundException("village.notFound", $"Village \"{slug}\" was not found.", new Dictionary<string, object> { ["slug"] = slug });
        }

        throw new UnauthorizedAccessException();
    }

    private static async Task<Member?> FindMemberAsync(DesertodDbContext db, string slug, Guid accountId, CancellationToken cancellationToken)
    {
        var village = await db.Villages.AsNoTracking()
            .Where(v => v.Slug == slug)
            .Select(v => new { v.Id, v.Slug, v.OwnerAccountId })
            .SingleOrDefaultAsync(cancellationToken);
        if (village is null)
        {
            return null;
        }

        if (village.OwnerAccountId == accountId)
        {
            var owner = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == accountId, cancellationToken);
            return new Member(village.Id, village.Slug, null, owner.Character?.Name ?? owner.DisplayName);
        }

        var citizen = await db.Citizens.AsNoTracking()
            .Where(c => c.VillageId == village.Id && c.AccountId == accountId)
            .Select(c => new { c.Id, c.Name })
            .SingleOrDefaultAsync(cancellationToken);
        return citizen is null ? null : new Member(village.Id, village.Slug, citizen.Id, citizen.Name);
    }

    private static ChatMessageDto ToDto(ChatMessage message) =>
        new(message.Id, message.AuthorName, message.CitizenId, message.IsTsar, message.Text, message.SentAt);
}
