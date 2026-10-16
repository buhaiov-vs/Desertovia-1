using Desertod.Domain.Rules;
using System.Text.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
namespace Desertod.Api.Tests;

public sealed class PlayerFeedback077MigrationTests
{
    [Fact]
    public async Task Legacy_buildings_get_stable_numbers_without_changing_ownership_goods_or_paid_work()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync();
        var options=new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db=new DesertodDbContext(options);await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;var owner=new Account{DisplayName="Owner",CreatedAt=now};db.Accounts.Add(owner);
        var village=Village.Create("legacy-feedback","Legacy",owner.Id,now);
        var home=new Structure{Kind=BuildingKind.Hut,PlotId="1,1",OwnerAccountId=owner.Id,BuiltAt=now,Damage=23};village.Structures.Add(home);
        var storage=StorageBuilding.Create(StorageKind.Heap,"5,1",now);storage.Put(ResourceKind.Wood,7);village.Storages.Add(storage);
        village.Sites.Add(new(){Kind=BuildingKind.Hut,PlotId=home.PlotId,PlacedAt=now,TargetLevel=2,Progress=[new(){Stage=1,JobId="bring-logs",Done=5}]});
        var citizen=CharacterCreation.Create(village.Id,owner.Id,"Resident",now);citizen.Renown=73;citizen.HomePlotId=home.PlotId;citizen.Carry(ResourceKind.Food,2);
        db.Villages.Add(village);db.Citizens.Add(citizen);await db.SaveChangesAsync();var stats=JsonSerializer.Serialize(citizen.Stats);db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync("20261016161632_OwnerCombatSiege");
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Villages" SET
                "Structures"=(SELECT jsonb_agg(s-'Number') FROM jsonb_array_elements("Structures") s),
                "Storages"=(SELECT jsonb_agg(s-'Number') FROM jsonb_array_elements("Storages") s),
                "Sites"=(SELECT jsonb_agg(s-'Number') FROM jsonb_array_elements("Sites") s);
            """);
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        Assert.False(db.Database.HasPendingModelChanges());
        var restored=await db.Villages.SingleAsync();var restoredHome=Assert.Single(restored.Structures);var restoredSite=Assert.Single(restored.Sites);
        Assert.Equal(2,restored.BuildingSequence);Assert.True(restoredHome.Number>0);Assert.Equal(restoredHome.Number,restoredSite.Number);
        Assert.NotEqual(restoredHome.Number,Assert.Single(restored.Storages).Number);Assert.Equal(owner.Id,restoredHome.OwnerAccountId);Assert.Equal(23,restoredHome.Damage);
        Assert.Equal(7,restored.Stock(ResourceKind.Wood));Assert.Equal(5,restoredSite.Progress.Single().Done);
        var saved=await db.Citizens.SingleAsync();Assert.Equal(73,saved.Renown);Assert.Equal(stats,JsonSerializer.Serialize(saved.Stats));Assert.Equal(2,saved.Carried(ResourceKind.Food));Assert.Null(saved.CurrentTargetPlotId);
        var number=restoredHome.Number;await db.SaveChangesAsync();db.ChangeTracker.Clear();Assert.Equal(number,(await db.Villages.SingleAsync()).Structures.Single().Number);
    }
}
