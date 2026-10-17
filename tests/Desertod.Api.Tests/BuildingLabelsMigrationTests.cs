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

public sealed class BuildingLabelsMigrationTests
{
    [Fact]
    public async Task Display_backfill_preserves_global_ids_goods_owners_and_sequence_and_survives_new_allocations()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync();
        var options=new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db=new DesertodDbContext(options);await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;var owner=new Account{DisplayName="Owner",CreatedAt=now};db.Accounts.Add(owner);
        var v=Village.Create("number-backfill","Numbers",owner.Id,now);
        v.Structures=[new(){Kind=BuildingKind.Hut,PlotId="0,0",Number=50,OwnerAccountId=owner.Id},
            new(){Kind=BuildingKind.Library,PlotId="4,0",Number=51},new(){Kind=BuildingKind.Hut,PlotId="8,0",Number=52}];
        var store=StorageBuilding.Create(StorageKind.WoodPile,"0,4",now);store.Number=53;store.Put(ResourceKind.Wood,17);v.Storages.Add(store);
        v.BuildingSequence=70;db.Villages.Add(v);await db.SaveChangesAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261017103201_RaidParticipation0711");
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        Assert.False(db.Database.HasPendingModelChanges());
        var loaded=await db.Villages.SingleAsync(x=>x.Id==v.Id);
        var labels=BuildingLabels.Visible(loaded);
        Assert.Equal(1,labels[50]);Assert.Equal(2,labels[52]);Assert.Equal(0,labels[51]);Assert.Equal(0,labels[53]);
        Assert.Equal(70,loaded.BuildingSequence);Assert.Equal(17,loaded.Storages.Single().Amount(ResourceKind.Wood));
        Assert.Equal(owner.Id,loaded.Structures.Single(s=>s.Number==50).OwnerAccountId);
        loaded.Structures.Remove(loaded.Structures.Single(s=>s.Number==50));
        loaded.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="12,0"});await db.SaveChangesAsync();db.ChangeTracker.Clear();
        loaded=await db.Villages.SingleAsync(x=>x.Id==v.Id);labels=BuildingLabels.Visible(loaded);
        Assert.Equal(2,labels[52]);Assert.Equal(3,labels[71]);Assert.Equal(71,loaded.BuildingSequence);
    }
}
