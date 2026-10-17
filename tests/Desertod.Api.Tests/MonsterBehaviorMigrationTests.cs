using Desertod.Api.Game;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Desertod.Api.Tests;

public sealed class MonsterBehaviorMigrationTests
{
    [Fact]
    public async Task Old_active_json_loads_without_healing_and_new_windup_survives_a_reload()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync();
        var options=new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db=new DesertodDbContext(options);await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;
        var owner=new Account{DisplayName="Owner",CreatedAt=now};db.Accounts.Add(owner);
        var village=Village.Create("legacy-monsters","Legacy",owner.Id,now);db.Villages.Add(village);
        var battle=new RaidBattle{VillageId=village.Id,Sector="west",Phase="Fighting",StartsAt=now,LastTickAt=now,
            EndsAt=now.AddMinutes(4),DamageDone=17,Units=[new(){Health=7,MaximumHealth=30,X=15.5,Y=13.5}],
            Fighters=[new(){CitizenId=Guid.NewGuid(),Health=13,MaximumHealth=40,Arrows=3,DamageDealt=21}]};
        db.RaidBattles.Add(battle);await db.SaveChangesAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261016223103_PlayerFeedback077");
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "RaidBattles" SET "Units"=(SELECT jsonb_agg(u-'Enemy'-'BossPhase'-'StrikeX'-'StrikeY'-'StrikeTarget'-'StrikeAt'-'SpecialReadyAt')
                FROM jsonb_array_elements("Units") u);
            """);
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();
        Assert.False(db.Database.HasPendingModelChanges());
        var preservedVillage=await db.Villages.SingleAsync(v=>v.Id==village.Id);
        Assert.Equal(1,preservedVillage.RaidAdaptation.Coefficient);
        Assert.Empty(preservedVillage.RaidAdaptation.Recent);Assert.Empty(preservedVillage.RaidPresets);
        var restored=await db.RaidBattles.SingleAsync();var unit=Assert.Single(restored.Units);
        Assert.Equal(7,unit.Health);Assert.Equal(30,unit.MaximumHealth);Assert.Null(unit.StrikeAt);Assert.Null(unit.BossPhase);Assert.Null(unit.Enemy);
        Assert.Equal(17,restored.DamageDone);Assert.Equal(13,restored.Fighters.Single().Health);
        Assert.Equal(3,restored.Fighters.Single().Arrows);Assert.Equal(21,restored.Fighters.Single().DamageDealt);
        unit.Variant="boss";unit.BossPhase=2;unit.StrikeX=12.5;unit.StrikeY=13.5;unit.StrikeTarget="11,10";
        unit.StrikeAt=now.AddSeconds(3);unit.SpecialReadyAt=now.AddSeconds(10);
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var loaded=await db.RaidBattles.SingleAsync();var dto=loaded.ToBattleDto();
        Assert.Equal(2,dto.Units.Single().BossPhase);Assert.Equal(12.5,dto.Units.Single().StrikeX);
        Assert.InRange(Math.Abs((dto.Units.Single().StrikeAt!.Value-unit.StrikeAt.Value).TotalMilliseconds),0,1);
        Assert.Equal("11,10",loaded.Units.Single().StrikeTarget);Assert.Equal(7,loaded.Units.Single().Health);
    }
}
