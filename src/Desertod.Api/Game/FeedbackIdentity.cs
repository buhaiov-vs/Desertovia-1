using System.Globalization;
using Desertod.Api.Auth;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Desertod.Api.Game;

public sealed record FeedbackIdentityRequest(long TelegramUserId);
public sealed record FeedbackIdentityReply(bool Linked, Guid? AccountId = null, Guid? CitizenId = null,
    string? CharacterName = null, string? VillageSlug = null);

/// <summary>Привязка автора отзыва: только чтение, по подтверждённому Telegram ID, без поиска по нику.</summary>
public static class FeedbackIdentityEndpoints
{
    public static void MapFeedbackIdentityEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/feedback/identity", async (FeedbackIdentityRequest request, HttpContext http,
            IOptions<TelegramOptions> options, IDbContextFactory<DesertodDbContext> contexts,
            IConfiguration configuration, CancellationToken ct) =>
        {
            if (!AlmightyService.Trusted(http, options)) return Results.Unauthorized();
            if (request.TelegramUserId is <= 0 or > 9007199254740991L) return Results.BadRequest();
            http.Response.Headers.CacheControl = "no-store";
            var telegram = request.TelegramUserId.ToString(CultureInfo.InvariantCulture);
            await using var db = await contexts.CreateDbContextAsync(ct);
            var account = await db.ExternalLogins.AsNoTracking()
                .Where(l => l.Provider == "telegram" && l.ProviderUserId == telegram)
                .Join(db.Accounts.AsNoTracking(), l => l.AccountId, a => a.Id,
                    (l, a) => new { a.Id, a.DisplayName, CharacterName = a.Character == null ? null : a.Character.Name })
                .SingleOrDefaultAsync(ct);
            if (account is null) return Results.Ok(new FeedbackIdentityReply(false));
            var primary = configuration["Hosting:PrimaryVillage"]?.Trim();
            var citizens = from c in db.Citizens.AsNoTracking()
                           join v in db.Villages.AsNoTracking() on c.VillageId equals v.Id
                           where c.AccountId == account.Id && c.Status == CitizenStatus.Active
                           select new { c.Id, c.Name, c.CreatedAt, VillageSlug = v.Slug };
            if (!string.IsNullOrWhiteSpace(primary)) citizens = citizens.Where(c => c.VillageSlug == primary);
            var citizen = await citizens.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).FirstOrDefaultAsync(ct);
            // Царь может не иметь Citizen: код аккаунта всё равно совпадает с кодом в настройках.
            var village = citizen?.VillageSlug ?? await db.Villages.AsNoTracking()
                .Where(v => v.OwnerAccountId == account.Id && (primary == null || primary == "" || v.Slug == primary))
                .OrderBy(v => v.Id).Select(v => v.Slug).FirstOrDefaultAsync(ct);
            return Results.Ok(new FeedbackIdentityReply(true, account.Id, citizen?.Id,
                citizen?.Name ?? account.CharacterName ?? account.DisplayName, village));
        });
}
