using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Economy;
using Desertod.Domain.Rules;
using Desertod.Domain.Technologies;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
namespace Desertod.Api.Tests;
public sealed class ProgressionMigrationTests
{
    [Fact] public async Task Legacy_research_and_profiles_are_preserved_and_new_meal_state_starts_empty()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync();
        await using var db=new DesertodDbContext(new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        await db.Database.MigrateAsync();var now=DateTimeOffset.UtcNow;var account=new Account{DisplayName="Owner",CreatedAt=now};db.Accounts.Add(account);
        var v=Village.Create("legacy-meals","Legacy",account.Id,now);v.Knowledge=23;v.Research.Add(new(){TechnologyId=TechnologyCatalog.Writing,Done=3,Hands=[new(){CitizenId=Guid.NewGuid()}]});db.Villages.Add(v);
        var c=CharacterCreation.Create(v.Id,account.Id,"Resident",now);c.Renown=71;c.Stats.StrengthExperience=37;c.InjuredUntil=now.AddHours(1);PersonalInventory.Add(c,ResourceKind.Gold,7,91);db.Citizens.Add(c);await db.SaveChangesAsync();
        var savedInjury=await db.Citizens.AsNoTracking().Where(x=>x.Id==c.Id).Select(x=>x.InjuredUntil).SingleAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261017111120_FortificationLevels0711");
        await db.Database.ExecuteSqlRawAsync("""UPDATE "Villages" SET "Research"=(SELECT jsonb_agg(r-'KnowledgeWork') FROM jsonb_array_elements("Research") r);""");
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();Assert.False(db.Database.HasPendingModelChanges());
        var city=await db.Villages.SingleAsync(x=>x.Id==v.Id);Assert.Equal(23,city.Knowledge);var research=Assert.Single(city.Research);Assert.Equal(3,research.Done);Assert.Single(research.Hands);Assert.Equal(0,research.KnowledgeWork);Assert.Null(research.ResearchedAt);
        var restored=await db.Citizens.SingleAsync(x=>x.Id==c.Id);Assert.Equal(71,restored.Renown);Assert.Equal(37,restored.Stats.StrengthExperience);Assert.Equal(7,PersonalInventory.Amount(restored,ResourceKind.Gold));Assert.Equal(91,Assert.Single(restored.PersonalGoods).CostBasis);Assert.Equal(savedInjury,restored.InjuredUntil);Assert.Null(restored.MealKind);Assert.Null(restored.MealUntil);Assert.Null(restored.LastMealAt);
    }
}
