using Desertod.Api.Auth;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Accounts;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

/// <summary>Создаёт только настроенное царство владельца, без тестовых игроков и выдуманного прогресса.</summary>
public static class LaunchVillageSetup
{
    public static async Task EnsureAsync(IServiceProvider services, IConfiguration configuration)
    {
        var telegramId = configuration["Hosting:InitialTelegramOwner"];
        if (string.IsNullOrWhiteSpace(telegramId)) return;
        if (!long.TryParse(telegramId, out var number) || number <= 0) throw new InvalidOperationException("Invalid initial owner.");
        await using var scope = services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        var account = await accounts.ResolveExternalLoginAsync(AuthenticationSetup.TelegramProvider, telegramId,
            "Дезертод", CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<DesertodDbContext>();
        var village = await db.Villages.SingleOrDefaultAsync(v => v.Slug == "dezertovia");
        if (village is not null)
        {
            if (village.OwnerAccountId != account.Id) throw new InvalidOperationException("Initial village has a different owner.");
            return;
        }
        db.Villages.Add(Village.Create("dezertovia", "Дезертовия", account.Id, scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow()));
        await db.SaveChangesAsync();
    }
}
