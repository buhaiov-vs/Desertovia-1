using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
namespace Desertod.Api.Tests;

public class CooperativeMigrationTests
{
    [Fact]
    public async Task Legacy_bridge_goods_and_skill_progress_survive_the_additive_upgrade()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync();
        var options=new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        var now=DateTimeOffset.UtcNow;
        Guid villageId,personId;
        await using(var db=new DesertodDbContext(options))
        {
            await db.Database.MigrateAsync();
            var account=new Account{DisplayName="Migration test",CreatedAt=now};db.Accounts.Add(account);
            var village=Village.Create("legacy-check","Legacy check",account.Id,now);villageId=village.Id;
            var store=StorageBuilding.Create(StorageKind.WoodPile,"1,1",now);store.Goods.Add(new StoredGood{Kind=ResourceKind.Wood,Amount=70});village.Storages.Add(store);
            village.Structures.Add(new Structure{Kind=BuildingKind.Hut,PlotId="5,5",BuiltAt=now});db.Villages.Add(village);
            var citizen=CharacterCreation.Create(village.Id,account.Id,"Patient",now);personId=citizen.Id;
            citizen.Skills.Add(new SkillProgress{Kind=Skill.Building,Level=3,Experience=47});citizen.Renown=123;db.Citizens.Add(citizen);
            var bridge=new VillageProject{VillageId=village.Id,DefinitionId=ProjectCatalog.TsarBridge,Status=ProjectStatus.Completed,StartedAt=now,CompletedAt=now,SurveyProgress=10,BuildProgress=60};db.VillageProjects.Add(bridge);
            await db.SaveChangesAsync();db.ChangeTracker.Clear();
            await db.GetService<IMigrator>().MigrateAsync("20261014172144_AddExternalRecovery");
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "Villages" SET "Structures"=(SELECT jsonb_agg(item-'Investment'-'Level'-'ProductionCycles'-'Decommissioning') FROM jsonb_array_elements("Structures") item),
                    "Storages"=(SELECT jsonb_agg(item-'Investment'-'Decommissioning') FROM jsonb_array_elements("Storages") item);
                """);
            await db.Database.MigrateAsync();
        }
        await using var check=new DesertodDbContext(options);
        var city=await check.Villages.SingleAsync(v=>v.Id==villageId);
        var person=await check.Citizens.SingleAsync(c=>c.Id==personId);
        Assert.True(city.BridgeOpened);Assert.Equal(70,city.Stock(ResourceKind.Wood));Assert.Empty(city.Structures[0].Investment);Assert.Equal(1,city.Structures[0].Level);
        Assert.Equal(123,person.Renown);var skill=Assert.Single(person.Skills);Assert.Equal(3,skill.Level);Assert.Equal(47,skill.Experience);
        Assert.Equal(0,city.Storages[0].Put(ResourceKind.Wood,1));Assert.Equal(70,city.Storages[0].Take(ResourceKind.Wood,100));
    }
}
