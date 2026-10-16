using System.Text.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class Post071MigrationTests
{
    [Fact]
    public async Task Old_json_and_profiles_survive_migration_without_changing_owners_or_progress()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync();
        var options=new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db=new DesertodDbContext(options);await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;var owner=new Account{DisplayName="Tsar",CreatedAt=now};var account=new Account{DisplayName="Resident",CreatedAt=now};
        db.Accounts.AddRange(owner,account);
        var v=Village.Create("migration071","Migration",owner.Id,now);var person=CharacterCreation.Create(v.Id,account.Id,"Resident",now);person.Renown=37;
        v.Sites.Add(new(){Kind=BuildingKind.Hut,PlotId="26,5",OwnerAccountId=account.Id,PlacedAt=now,
            Progress=[new(){Stage=2,JobId="bring-logs",Done=9}]});
        var battle=new RaidBattle{Id=Guid.NewGuid(),VillageId=v.Id,Sector="north",Phase="Warning",StartsAt=now,LastTickAt=now,EndsAt=now.AddMinutes(5),
            Fighters=[new(){CitizenId=person.Id,Health=31,MaximumHealth=40,Guard=true,Active=true}]};
        db.Villages.Add(v);db.Citizens.Add(person);db.RaidBattles.Add(battle);await db.SaveChangesAsync();
        var stats=JsonSerializer.Serialize(person.Stats);db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync("20261015212416_LiveVillageCombat");
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Villages" SET "Sites"=(SELECT jsonb_agg(s-ARRAY['PlacedByAccountId','BoundaryApproved','PublicDemolition','Salvage']) FROM jsonb_array_elements("Sites") s);
            UPDATE "RaidBattles" SET "Fighters"=(SELECT jsonb_agg(f-ARRAY['AccountId','IsTsar','DestinationX','DestinationY']) FROM jsonb_array_elements("Fighters") f);
            """);
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        var restored=await db.Villages.AsNoTracking().SingleAsync();var site=Assert.Single(restored.Sites);
        Assert.Equal(account.Id,site.OwnerAccountId);Assert.Equal(9,Assert.Single(site.Progress).Done);Assert.Empty(site.Salvage);Assert.False(site.PublicDemolition);Assert.Empty(restored.SiteRecoveries);
        var saved=await db.Citizens.AsNoTracking().SingleAsync();Assert.Equal(account.Id,saved.AccountId);Assert.Equal(37,saved.Renown);Assert.Equal(stats,JsonSerializer.Serialize(saved.Stats));
        var fighter=Assert.Single((await db.RaidBattles.AsNoTracking().SingleAsync()).Fighters);Assert.False(fighter.IsTsar);Assert.Null(fighter.AccountId);Assert.Equal(person.Id,fighter.CitizenId);Assert.Equal(31,fighter.Health);
    }
}
