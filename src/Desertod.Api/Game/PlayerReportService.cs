using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Desertod.Api.Auth;
using Desertod.Domain;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

public sealed record ReportScreen(string? Page, string? Menu, string? Selection, string? DungeonId,
    double? X, double? Y, int? Width, int? Height, string? ClientVersion, string? Platform,
    double? FrameP95Ms, double? PacketGapMaxMs, int? Reconnects);
public sealed record PlayerReportRequest(Guid Id, string Kind, string Text, string? Slug,
    DateTimeOffset CapturedAt, ReportScreen? Screen, string? Screenshot);
public sealed record PlayerReportReceipt(Guid Id, string State, bool ScreenshotIncluded);
public sealed record PlayerReportNotice(Guid ReportId, string OwnerId, string Payload, string? Screenshot);

public static class PlayerReportEndpoints
{
    public static void MapPlayerReportEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/reports", async (PlayerReportRequest request, ClaimsPrincipal user,
            HttpContext http, PlayerReportService reports, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await reports.SubmitAsync(user.RequireAccountId(), request, ct));
        }).RequireAuthorization().WithMetadata(new RequestSizeLimitAttribute(550_000));
}

/// <summary>Independent report storage/outbox. Never takes a village simulation lock or advances gameplay.</summary>
public sealed class PlayerReportService(IDbContextFactory<DesertodDbContext> factory,
    IAlmightyBridge bridge, IConfiguration config, TimeProvider time, ILogger<PlayerReportService> logger)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static void Invalid() => throw new GameRuleException("report.invalid", "Check the report fields.");
    private static string? Bounded(string? value, int max)
    {
        if (value is null) return null;
        if (value.Length > max || value.Any(c => char.IsControl(c))) Invalid();
        return value;
    }
    private static double? Number(double? value, double max = 1_000_000) =>
        value is {} n && double.IsFinite(n) && Math.Abs(n) <= max ? n : null;

    public async Task<PlayerReportReceipt> SubmitAsync(Guid accountId, PlayerReportRequest request, CancellationToken ct)
    {
        if (request.Id == Guid.Empty || request.Kind is not ("bug" or "complaint" or "suggestion")
            || string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000
            || request.CapturedAt == default || request.CapturedAt > time.GetUtcNow().AddMinutes(10)) Invalid();
        var screen = request.Screen;
        // This typed allowlist deliberately excludes arbitrary logs, URLs, cookies, chat and storage.
        var safe = screen is null ? null : new ReportScreen(Bounded(screen.Page, 40), Bounded(screen.Menu, 80),
            Bounded(screen.Selection, 160), Bounded(screen.DungeonId, 40), Number(screen.X), Number(screen.Y),
            screen.Width is > 0 and <= 10000 ? screen.Width : null, screen.Height is > 0 and <= 10000 ? screen.Height : null,
            Bounded(screen.ClientVersion, 24), Bounded(screen.Platform, 80), Number(screen.FrameP95Ms),
            Number(screen.PacketGapMaxMs), screen.Reconnects is >= 0 and <= 100000 ? screen.Reconnects : null);
        byte[]? image = null;
        if (request.Screenshot is {} encoded)
        {
            if (encoded.Length > 480000) Invalid();
            try { image = Convert.FromBase64String(encoded); } catch (FormatException) { Invalid(); }
            // A bounded JPEG attachment, never a URL or an arbitrary file. Not served as executable web content.
            if (image is null || !ReportImage.Valid(image)) Invalid();
        }
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.Kind, text = request.Text.Trim(), request.Slug, request.CapturedAt, screen = safe,
                image = image is null ? null : Convert.ToHexString(SHA256.HashData(image)) }, Json))));
        await using var db = await factory.CreateDbContextAsync(ct);
        // Account lock serializes rate limit and idempotency across processes, without touching the village.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({accountId.ToString()}, 77215))", ct);
        var previous = await db.PlayerReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == request.Id, ct);
        if (previous is not null)
        {
            if (previous.AccountId != accountId) throw new UnauthorizedAccessException();
            if (previous.Fingerprint != fingerprint) throw new GameRuleException("report.changed", "This report ID was already submitted.");
            using var savedPayload = JsonDocument.Parse(previous.Payload);
            return new(previous.Id, "saved", savedPayload.RootElement.GetProperty("screenshotIncluded").GetBoolean());
        }
        var now = time.GetUtcNow();
        if (await db.PlayerReports.CountAsync(r => r.AccountId == accountId && r.CreatedAt > now.AddHours(-1), ct) >= 5)
            throw new GameRuleException("report.limit", "At most five reports per hour.");
        var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == accountId, ct);
        var slug = Bounded(request.Slug, 64) ?? config["Hosting:PrimaryVillage"];
        var village = slug is null ? null : await db.Villages.AsNoTracking().Where(v => v.Slug == slug)
            .Select(v => new { v.Id, v.Slug, v.OwnerAccountId, v.IsPrivate }).SingleOrDefaultAsync(ct);
        if (slug is not null && village is null) throw new NotFoundException("village.notFound", "Village not found.");
        var citizen = village is null ? null : await db.Citizens.AsNoTracking()
            .Where(c => c.VillageId == village.Id && c.AccountId == accountId)
            .Select(c => new { c.Id, c.Name, c.IsGovernor, c.Status, c.SpotX, c.SpotY, c.CurrentTaskId,
                c.StoredEnergy, c.InjuredUntil, c.HospitalizedAt }).FirstOrDefaultAsync(ct);
        if (village is { IsPrivate: true } && village.OwnerAccountId != accountId && citizen is null)
            throw new UnauthorizedAccessException();
        var telegramId = await db.ExternalLogins.AsNoTracking().Where(l => l.AccountId == accountId && l.Provider == "telegram")
            .Select(l => l.ProviderUserId).SingleOrDefaultAsync(ct);
        var villageId = village?.Id;
        var battles = await db.RaidBattles.AsNoTracking().Where(b => b.VillageId == villageId && b.FinishedAt == null)
            .OrderBy(b => b.Id).Take(12).Select(b => new { b.Id, b.Phase, b.Revision }).ToListAsync(ct);
        var screenshotOmitted = false;
        if (image is not null)
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(77215001)", ct);
            var used = await db.PlayerReports.Where(r => r.Screenshot != null).SumAsync(r => (long)r.Screenshot!.Length, ct);
            var storageLimit = Math.Clamp(config.GetValue<long?>("Game:ReportScreenshotStorageBytes") ?? 256L * 1024 * 1024, 0, 256L * 1024 * 1024);
            if (used + image.Length > storageLimit) { image = null; screenshotOmitted = true; }
        }
        var payload = JsonSerializer.Serialize(new {
            format = 1, reportId = request.Id, kind = request.Kind, text = request.Text.Trim(),
            capturedAt = request.CapturedAt, receivedAt = now,
            author = new { accountId, account.DisplayName, telegramId, citizen },
            village = village is null ? null : new { village.Id, village.Slug },
            server = new { release = config["Hosting:ReleaseSha"] },
            screen = safe, battles,
            screenshotIncluded = image is not null, screenshotOmittedForStorageLimit = screenshotOmitted
        }, Json);
        db.PlayerReports.Add(new PlayerReport { Id = request.Id, AccountId = accountId, VillageId = village?.Id,
            Kind = request.Kind, Text = request.Text.Trim(), Fingerprint = fingerprint, Payload = payload,
            Screenshot = image, CreatedAt = now, NextAttemptAt = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(request.Id, "saved", image is not null);
    }

    public async Task TickAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = time.GetUtcNow();
        // Delivered images expire after seven days; undelivered reports retain their attachment.
        await db.PlayerReports.Where(r => r.DeliveryState == "Sent" && r.CreatedAt < now.AddDays(-7) && r.Screenshot != null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Screenshot, (byte[]?)null), ct);
        var report = await db.PlayerReports.AsNoTracking().Where(r =>
            (r.DeliveryState == "Queued" || r.DeliveryState == "Unavailable" || r.DeliveryState == "Sending" && r.AttemptAt < now.AddMinutes(-3))
            && r.NextAttemptAt <= now).OrderBy(r => r.CreatedAt).FirstOrDefaultAsync(ct);
        if (report is null) return;
        var claimed = await db.PlayerReports.Where(r => r.Id == report.Id && r.Attempts == report.Attempts)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DeliveryState, "Sending").SetProperty(r => r.AttemptAt, now)
                .SetProperty(r => r.Attempts, report.Attempts + 1), ct);
        if (claimed == 0) return;
        var owner = await db.Villages.Where(v => v.Id == report.VillageId).Select(v => (Guid?)v.OwnerAccountId).SingleOrDefaultAsync(ct);
        var ownerId = await db.ExternalLogins.Where(l => l.AccountId == owner && l.Provider == "telegram")
            .Select(l => l.ProviderUserId).SingleOrDefaultAsync(ct);
        var state = "Unavailable";
        if (ownerId is not null)
        {
            try { state = (await bridge.NotifyReportAsync(new(report.Id, ownerId, report.Payload,
                report.Screenshot is null ? null : Convert.ToBase64String(report.Screenshot)), ct)).State; }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
            { logger.LogWarning("Report bridge unavailable ({Type}).", e.GetType().Name); }
        }
        if (state is not ("Sent" or "Unknown" or "Unavailable")) state = "Unavailable";
        await db.PlayerReports.Where(r => r.Id == report.Id && r.Attempts == report.Attempts + 1)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.DeliveryState, state)
                .SetProperty(r => r.NextAttemptAt, now.AddMinutes(Math.Min(30, Math.Pow(2, Math.Min(report.Attempts, 5))))), ct);
    }
}

public sealed class PlayerReportWorker(PlayerReportService reports, TimeProvider time, ILogger<PlayerReportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            try { await reports.TickAsync(stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException)
            { logger.LogWarning("Report delivery tick failed ({Type}).", e.GetType().Name); }
    }
}
