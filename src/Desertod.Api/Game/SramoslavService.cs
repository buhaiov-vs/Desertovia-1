using Desertod.Domain;
using Desertod.Api.Auth;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Desertod.Api.Game;

public sealed record SramoslavReply(string Text, bool Ai);
public sealed record SramoslavQuestion(string? Text);

/// <summary>The chaplain can speak; he has no game commands or access to wallets.</summary>
public sealed class SramoslavService(ChatService chat, IDbContextFactory<DesertodDbContext> factory,
    IServiceScopeFactory scopes, IMemoryCache cache)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<SramoslavReply> AskAsync(string slug, Guid accountId, string? question, CancellationToken cancellationToken)
    {
        if (!await chat.IsMemberAsync(slug, accountId, cancellationToken))
            throw new GameRuleException("chat.notMember", "Join the village before talking to the chaplain.");
        var text = question?.Trim() ?? "";
        if (text.Length is < 1 or > 300) throw new GameRuleException("chat.text", "Write 1 to 300 characters.");
        if (!await gate.WaitAsync(0, cancellationToken)) return new("Чадо, подожди немного — отвечаю соседу.", false);
        try
        {
            var key = "sramoslav:" + accountId;
            if (cache.TryGetValue(key, out _)) return new("Чадо, дай мне немного отдышаться. Спроси через полминуты.", false);
            cache.Set(key, true, TimeSpan.FromSeconds(30));
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var village = await db.Villages.AsNoTracking().SingleAsync(v => v.Slug == slug, cancellationToken);
            using var scope = scopes.CreateScope();
            var bridge = scope.ServiceProvider.GetRequiredService<TelegramBrowserBridge>();
            if (!bridge.Enabled) return new("Чадо, я пока отдыхаю. Скоро снова поговорим.", false);
            try
            {
                return await bridge.CallAsync<SramoslavReply>("speak", new
                {
                    text,
                    context = new { village = village.Name, buildings = village.Structures.Select(s => s.Kind.ToString()).ToArray(),
                        note = "Rules are those of the current game. Do not invent costs, rewards or commands." }
                }, cancellationToken);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            { return new("Чадо, связь сейчас подводит. Дай мне минутку и спроси снова.", false); }
        }
        finally { gate.Release(); }
    }
}
