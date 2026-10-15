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
    private static readonly TimeSpan AmbientGap = TimeSpan.FromMinutes(20);
    private const int HourlyLimit = 12;
    private readonly Queue<DateTimeOffset> attempts=new();
    private readonly Dictionary<Guid,DateTimeOffset> seenCalls=[];
    private readonly Dictionary<Guid,DateTimeOffset> ambientAttempts=[];
    private DateTimeOffset lastAttempt=DateTimeOffset.MinValue;

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
            while(attempts.TryPeek(out var oldAttempt)&&oldAttempt<=hourAgo)attempts.Dequeue();
            if(attempts.Count>=HourlyLimit||now-lastAttempt<Gap)return;
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
                    && now - m.SentAt < Gap && m.SentAt>seenCalls.GetValueOrDefault(village.Id) && IsCalled(m.Text));
                string? text = null;
                string? topic = null;
                if (target is not null)
                {
                    seenCalls[village.Id]=target.SentAt;lastAttempt=now;attempts.Enqueue(now);
                    var context = recent.Take(8).Reverse().Select(m => new { name = m.AuthorName, text = m.Text }).ToArray();
                    text = await priest.AnswerChatAsync(village.Name, target.AuthorName, target.Text, context,
                        village.Structures.Select(s => s.Kind.ToString()).ToArray(), cancellationToken);
                }
                else if (now - lastBot >= AmbientGap && now-ambientAttempts.GetValueOrDefault(village.Id)>=AmbientGap && presence.IsVillagePresent(village.Slug)
                    && recent.Count(m => now-m.SentAt < TimeSpan.FromMinutes(5)) >= 3
                    && recent.Any(m => m.SentAt > lastBot))
                {
                    var history = recent.Take(8).Reverse().Select(m=>new {name=m.AuthorName,text=m.Text}).ToArray();
                    topic=village.Sites.Count>0 ? "construction" : village.Structures.Any(b=>b.Damage>50) ? "repair" : null;
                    if(topic is not null && await db.ChaplainMessages.AnyAsync(m=>m.VillageId==village.Id && m.SentAt>hourAgo && m.Topic==topic,cancellationToken))continue;
                    var facts = village.Sites.Count > 0 ? $"В деревне {village.Sites.Count} незавершённых строек."
                        : village.Structures.Any(b=>b.Damage>50) ? "В деревне есть повреждённые здания."
                        : "";
                    // No private clock/location enters the public prompt. The bot owns its private tone reference.
                    if (facts.Length > 0)
                    {
                        ambientAttempts[village.Id]=now;lastAttempt=now;attempts.Enqueue(now);
                        text = await priest.AnswerChatAsync(village.Name, "деревня",
                            "Коротко откликнись на текущую беседу; можно промолчать. Проверенный факт: " + facts,
                            history, village.Structures.Select(s=>s.Kind.ToString()).ToArray(),cancellationToken);
                    }
                }
                if (!ChaplainSpeech.IsPlainText(text))
                {
                    if (!string.IsNullOrWhiteSpace(text)) logger.LogWarning("Chaplain service output withheld from public chat.");
                    continue;
                }
                var lastLines = await db.ChaplainMessages.AsNoTracking().Where(m=>m.VillageId==village.Id)
                    .OrderByDescending(m=>m.SentAt).Take(10).Select(m=>m.Text).ToListAsync(cancellationToken);
                if (lastLines.Any(old => string.Equals(ShortLine(old), ShortLine(text), StringComparison.OrdinalIgnoreCase))) continue;
                var line = ChaplainMessage.Speak(village.Id, ShortLine(text), time.GetUtcNow());
                line.Topic=topic;
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
