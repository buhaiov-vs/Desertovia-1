using System.Text.RegularExpressions;
using Desertod.Domain.Chat;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

/// <summary>Short shared village speech, with a durable flood budget in chat history.</summary>
public sealed partial class ChaplainChatWorker(IDbContextFactory<DesertodDbContext> factory, SramoslavService priest,
    CitizenPresence presence, VillageNotifier notifier, TimeProvider time, IConfiguration config, ILogger<ChaplainChatWorker> logger)
    : BackgroundService
{
    private readonly DateTimeOffset startedAt = time.GetUtcNow();
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan AmbientGap = TimeSpan.FromMinutes(8);
    private const int HourlyLimit = 12;

    [GeneratedRegex(@"(?i)(?<![\p{L}\p{N}_])(?:батюшк[аеиуо]|срамослав(?:а|у)?|@?sramoslav(?:_bot)?|отче)(?![\p{L}\p{N}_])")]
    private static partial Regex CallPattern();

    public static bool IsCalled(string text) => CallPattern().IsMatch(text);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await TickAsync(stoppingToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            { logger.LogError(exception, "Village chaplain chat tick failed."); }
        }
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var primary = config["Hosting:PrimaryVillage"];
        if (!(config.GetValue<bool?>("Game:ChaplainChat:Enabled") ?? !string.IsNullOrEmpty(primary))) return;
        if (!await gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var now = time.GetUtcNow();
            var hourAgo = now.AddHours(-1);
            // All NPC lines are persisted. Restarting does not reset the shared hourly budget.
            var botTimes = await db.ChaplainMessages.AsNoTracking().Where(m => m.SentAt > hourAgo)
                .Select(m => m.SentAt).ToListAsync(cancellationToken);
            if (botTimes.Count >= HourlyLimit || botTimes.Any(at => now - at < Gap)) return;
            var villages = await db.Villages.AsNoTracking().Where(v => primary == null || primary == "" || v.Slug == primary)
                .ToListAsync(cancellationToken);
            foreach (var village in villages)
            {
                var recentSince = now.AddMinutes(-10);
                var recent = await db.ChatMessages.AsNoTracking().Where(m => m.VillageId == village.Id && m.SentAt >= recentSince)
                    .OrderByDescending(m => m.SentAt).Take(50).ToListAsync(cancellationToken);
                var lastBot = await db.ChaplainMessages.AsNoTracking().Where(m => m.VillageId == village.Id)
                    .MaxAsync(m => (DateTimeOffset?)m.SentAt, cancellationToken) ?? startedAt;
                var target = recent.FirstOrDefault(m => m.SentAt >= startedAt && m.SentAt > lastBot
                    && now - m.SentAt < Gap && IsCalled(m.Text));
                string? text = null;
                if (target is not null)
                {
                    var context = recent.Take(8).Reverse().Select(m => new { name = m.AuthorName, text = m.Text }).ToArray();
                    text = await priest.AnswerChatAsync(village.Name, target.AuthorName, target.Text, context,
                        village.Structures.Select(s => s.Kind.ToString()).ToArray(), cancellationToken);
                }
                else if (now - lastBot >= AmbientGap && presence.IsVillagePresent(village.Slug))
                {
                    // No unsolicited speech in an empty village or when everyone is simply idle.
                    var working = await db.Citizens.AnyAsync(c => c.VillageId == village.Id && c.CurrentTaskId != null && c.CycleEndsAt > now, cancellationToken);
                    var chatting = recent.Count(m => now - m.SentAt < TimeSpan.FromMinutes(5)) >= 3;
                    if (working || chatting)
                    {
                        string[] lines = ["Дезертовия растёт общими руками. Договаривайтесь, кому нужна помощь, чада!",
                            "Устали — отдохните, чада. С пустыми силами и кадило тяжёлое.",
                            "Каждая полезная работа двигает деревню вперёд. Держимся вместе, чада!"];
                        text = lines[Random.Shared.Next(lines.Length)];
                    }
                }
                if (string.IsNullOrWhiteSpace(text)) continue;
                var line = ChaplainMessage.Speak(village.Id, ShortLine(text), time.GetUtcNow());
                db.ChaplainMessages.Add(line);
                await db.SaveChangesAsync(cancellationToken);
                await notifier.ChatMessageSent(village.Slug, ChatService.ToDto(line));
                return; // One shared line per tick, never one for every browser tab.
            }
        }
        finally { gate.Release(); }
    }

    public static string ShortLine(string text)
    {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length <= ChatMessage.TextMaximumLength) return text;
        var end = ChatMessage.TextMaximumLength - 1;
        if (char.IsHighSurrogate(text[end - 1])) end--;
        var prefix = text[..end];
        var space = prefix.LastIndexOf(' ');
        if (space > 70) prefix = prefix[..space];
        return prefix.TrimEnd('.', ',', ':', ';') + "…";
    }
}
