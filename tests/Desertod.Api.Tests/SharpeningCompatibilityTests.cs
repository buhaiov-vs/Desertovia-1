using System.Net.Http.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class SharpeningMigrationTests
{
    [Fact]
    public async Task Legacy_json_profiles_and_paid_work_survive_then_mixed_edges_round_trip()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17").Build();
        await postgres.StartAsync();
        await using var db = new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var account = new Account { DisplayName = "Owner", CreatedAt = now };
        db.Accounts.Add(account);
        var village = Village.Create("old-sharpen", "Legacy", account.Id, now);
        var storage = StorageBuilding.Create(StorageKind.TownHall, "13,5", now);
        storage.Put(ResourceKind.Sword, 2); village.Storages.Add(storage); db.Villages.Add(village);
        var citizen = CharacterCreation.Create(village.Id, account.Id, "Resident", now);
        citizen.Carry(ResourceKind.Arrows, 4); citizen.Weapon = ResourceKind.Spear;
        citizen.Renown = 73; citizen.Stats.StrengthExperience = 41;
        citizen.IsHungry = true; citizen.CurrentTaskId = "forage";
        citizen.CycleSettled = false; citizen.CycleEndsAt = now.AddMinutes(2);
        db.Citizens.Add(citizen); await db.SaveChangesAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261017114745_ProgressionAndMeals0711");
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Citizens" SET "Load"=(SELECT jsonb_agg(g-'Sharpen') FROM jsonb_array_elements("Load") g);
            UPDATE "Villages" SET "Storages"=(SELECT jsonb_agg(jsonb_set(s,ARRAY['Goods'],
              COALESCE((SELECT jsonb_agg(g-'Sharpen') FROM jsonb_array_elements(s->'Goods') g),'[]'::jsonb)))
              FROM jsonb_array_elements("Storages") s);
            """);
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.False(db.Database.HasPendingModelChanges());
        var restored = await db.Citizens.SingleAsync();
        Assert.Equal(73, restored.Renown); Assert.Equal(41, restored.Stats.StrengthExperience);
        Assert.Equal(0, restored.WeaponSharpen); Assert.Equal(0, restored.ArrowSharpen);
        Assert.Null(restored.CycleHungry); Assert.False(restored.CycleSettled);
        Assert.Equal("forage", restored.CurrentTaskId); Assert.True(restored.IsHungry);
        Assert.Equal(4, Assert.Single(restored.Load).Amount); Assert.Equal(0, restored.Load[0].Sharpen);
        var city = await db.Villages.SingleAsync();
        Assert.Equal(2, city.Stock(ResourceKind.Sword));
        restored.Carry(ResourceKind.Arrows, 2, 3); restored.WeaponSharpen = 2; restored.CycleHungry = true;
        city.Store(ResourceKind.Sword, 1, 3); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var reread = await db.Citizens.SingleAsync();
        Assert.Equal(2, reread.WeaponSharpen); Assert.True(reread.CycleHungry);
        Assert.Equal(6, reread.Carried(ResourceKind.Arrows)); Assert.Equal(2, reread.Load.Single(g => g.Sharpen == 3).Amount);
        Assert.Equal(3, (await db.Villages.SingleAsync()).Stock(ResourceKind.Sword));
    }
}

[Collection(ApiCollection.Name)]
public sealed class SharpeningApiTests(DesertodApiFactory factory)
{
    [Fact]
    public async Task Buying_plain_stock_never_consumes_or_downgrades_sharpened_goods()
    {
        var players = new Players(factory);
        var city = await players.FoundVillageAsync("Sharp stock");
        var player = await players.JoinAsync(city, "Smith");
        await Players.OkJsonAsync(await player.Client.GetAsync(city.Path + "/economy"));
        await using (var db = await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync())
        {
            var v = await db.Villages.SingleAsync(v => v.Slug == city.Slug);
            v.Storages.Add(StorageBuilding.Create(StorageKind.TownHall, "13,5", factory.Clock.GetUtcNow()));
            Assert.Equal(1, v.Store(ResourceKind.Sword, 1, 3));
            var citizen = await db.Citizens.SingleAsync(c => c.Id == player.Id);
            var wallet = await db.GameWallets.SingleAsync(w => w.AccountId == citizen.AccountId && w.VillageId == v.Id);
            wallet.Coins = 1000; await db.SaveChangesAsync();
        }
        await Players.AssertRuleBrokenAsync(await player.Client.PostAsJsonAsync(city.Path + "/economy",
            new { requestId = Guid.NewGuid(), action = "buy-stock", resource = "Sword", amount = 1 }), "inventory.stockMissing");
        await players.ArrangeVillageAsync(city.Slug, (v, now) => Assert.Equal(1, v.Store(ResourceKind.Sword, 1)));
        await Players.OkJsonAsync(await player.Client.PostAsJsonAsync(city.Path + "/economy",
            new { requestId = Guid.NewGuid(), action = "buy-stock", resource = "Sword", amount = 1 }));
        await using var check = await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var kept = await check.Villages.SingleAsync(v => v.Slug == city.Slug);
        Assert.Equal(1, kept.Stock(ResourceKind.Sword));
        Assert.Equal(3, kept.Storages.SelectMany(s => s.Goods).Single(g => g.Kind == ResourceKind.Sword).Sharpen);
    }
}
