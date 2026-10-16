using Desertod.Domain.Rules;
using System.Text.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
namespace Desertod.Api.Tests;
public sealed class OwnerCorrectionMigrationTests
{
    [Fact]
    public async Task Old_sites_fighters_and_character_progress_survive_without_a_new_castle_or_free_health()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync();
        var options=new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db=new DesertodDbContext(options);await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;var owner=new Account{DisplayName="Owner",CreatedAt=now};db.Accounts.Add(owner);
        var v=Village.Create("old-owner-world","Old world",owner.Id,now);
        var citizen=CharacterCreation.Create(v.Id,owner.Id,"Resident",now);citizen.Renown=73;
        v.Sites.Add(new(){Kind=BuildingKind.Hut,PlotId="1,1",OwnerAccountId=owner.Id,Progress=[new(){Stage=1,JobId="bring-logs",Done=7}]});
        var battle=new RaidBattle{VillageId=v.Id,Sector="north",Fighters=[new(){CitizenId=citizen.Id,Health=13,MaximumHealth=40,Active=true,Mode="hold"}]};
        db.Villages.Add(v);db.Citizens.Add(citizen);db.RaidBattles.Add(battle);await db.SaveChangesAsync();
        var stats=JsonSerializer.Serialize(citizen.Stats);db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync("20261016080309_CivicTelegramNotices");
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Villages" SET "Sites"=(SELECT jsonb_agg(s-ARRAY['Damage','RuinedAt']) FROM jsonb_array_elements("Sites") s);
            UPDATE "RaidBattles" SET "Fighters"=(SELECT jsonb_agg(f-ARRAY['HealReadyAt','HealingReceipts']) FROM jsonb_array_elements("Fighters") f);
            """);
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        var restored=await db.Villages.SingleAsync();Assert.Null(restored.DefeatedAt);Assert.DoesNotContain(restored.Structures,s=>s.Kind==BuildingKind.Castle);
        var site=Assert.Single(restored.Sites);Assert.Equal(0,site.Damage);Assert.Null(site.RuinedAt);Assert.Equal(owner.Id,site.OwnerAccountId);Assert.Equal(7,Assert.Single(site.Progress).Done);
        var fighter=Assert.Single((await db.RaidBattles.SingleAsync()).Fighters);Assert.Equal(13,fighter.Health);Assert.Equal("hold",fighter.Mode);Assert.Null(fighter.HealReadyAt);Assert.Null(fighter.HealingReceipts);
        var person=await db.Citizens.SingleAsync();Assert.Equal(73,person.Renown);Assert.Equal(stats,JsonSerializer.Serialize(person.Stats));
    }
}
