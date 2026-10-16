using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class FeedbackIdentityTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);
    private const string Route = "/api/feedback/identity";
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static void Trust(HttpClient client) => client.DefaultRequestHeaders.Add("X-Desertovia-Bridge",
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("desertovia-bridge:" + DesertodApiFactory.TelegramBotToken))));

    [Fact]
    public async Task Public_or_player_session_cannot_read_identity_without_bridge_authentication()
    {
        var village = await players.FoundVillageAsync("Private feedback");
        foreach (var client in new[] { factory.CreateClient(), village.Tsar })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Route, new { telegramUserId = 7111 })).StatusCode);
            client.DefaultRequestHeaders.Add("X-Desertovia-Bridge", "forged");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Route, new { telegramUserId = 7111 })).StatusCode);
        }
    }

    [Fact]
    public async Task Telegram_link_resolves_exact_account_despite_identical_names_without_mutating_progress()
    {
        var village = await players.FoundVillageAsync("Feedback mapping");
        var first = await players.JoinAsync(village, "Same name");
        var second = await players.JoinAsync(village, "Same name");
        Guid account;
        await using (var db = await Db())
        {
            var citizen = await db.Citizens.SingleAsync(c => c.Id == first.Id); account = citizen.AccountId;
            citizen.Renown = 73; citizen.StoredEnergy = 12; citizen.CyclesSinceMeal = 3;
            var other = await db.Citizens.SingleAsync(c => c.Id == second.Id);
            db.ExternalLogins.AddRange(new ExternalLogin { Provider="telegram", ProviderUserId="71107101", AccountId=account, ProviderUserName="renamed" },
                new ExternalLogin { Provider="telegram", ProviderUserId="71107102", AccountId=other.AccountId, ProviderUserName="same_name" });
            await db.SaveChangesAsync();
        }
        await using var configured = factory.WithWebHostBuilder(b => b.UseSetting("Authentication:Telegram:BotUsername", "test_bot"));
        using var client = configured.CreateClient(); Trust(client);
        // Переданный чужой код и старый ник не участвуют в определении отправителя.
        var response = await client.PostAsJsonAsync(Route, new { telegramUserId=71107101, accountId=Guid.NewGuid(), username="same_name" });
        var data = await OkJsonAsync(response);
        Assert.True(data.GetProperty("linked").GetBoolean()); Assert.Equal(account, data.GetProperty("accountId").GetGuid());
        Assert.Equal(first.Id, data.GetProperty("citizenId").GetGuid()); Assert.Equal("Same name", data.GetProperty("characterName").GetString());
        Assert.Equal(village.Slug, data.GetProperty("villageSlug").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(new[] {"accountId","characterName","citizenId","linked","villageSlug"}, data.EnumerateObject().Select(p=>p.Name).Order().ToArray());
        await using (var db = await Db())
        {
            var citizen = await db.Citizens.AsNoTracking().SingleAsync(c=>c.Id==first.Id);
            Assert.Equal(73,citizen.Renown); Assert.Equal(12,citizen.StoredEnergy); Assert.Equal(3,citizen.CyclesSinceMeal);
        }
        var missing = await client.PostAsJsonAsync(Route,new {telegramUserId=71107103});
        Assert.False((await OkJsonAsync(missing)).GetProperty("linked").GetBoolean());
        foreach (var bad in new long[] {0,-1,9007199254740992L})
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Route,new {telegramUserId=bad})).StatusCode);
    }

    [Fact]
    public async Task Linked_tsar_has_settings_account_code_even_without_a_citizen_row()
    {
        var village=await players.FoundVillageAsync("Royal report");Guid account;
        await using(var db=await Db())
        {
            account=(await db.Villages.SingleAsync(v=>v.Slug==village.Slug)).OwnerAccountId;
            db.ExternalLogins.Add(new(){Provider="telegram",ProviderUserId="71107104",AccountId=account});await db.SaveChangesAsync();
        }
        await using var configured=factory.WithWebHostBuilder(b=>b.UseSetting("Authentication:Telegram:BotUsername","test_bot"));
        using var client=configured.CreateClient();Trust(client);
        var data=await OkJsonAsync(await client.PostAsJsonAsync(Route,new{telegramUserId=71107104}));
        Assert.Equal(account,data.GetProperty("accountId").GetGuid());Assert.Equal(JsonValueKind.Null,data.GetProperty("citizenId").ValueKind);
        Assert.Equal(village.Slug,data.GetProperty("villageSlug").GetString());
    }
}
