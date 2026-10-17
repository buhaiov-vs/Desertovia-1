using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
namespace Desertod.Api.Tests;
public sealed class FortificationMigrationTests
{
    [Fact] public async Task Legacy_walls_keep_damage_gate_state_and_geometry_and_receive_only_the_wooden_tier()
    {
        await using var postgres=new PostgreSqlBuilder("postgres:17").Build();await postgres.StartAsync();
        var options=new DbContextOptionsBuilder<DesertodDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db=new DesertodDbContext(options);await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;var owner=new Account{DisplayName="Owner",CreatedAt=now};db.Accounts.Add(owner);
        var v=Village.Create("legacy-fence","Fence",owner.Id,now);v.Fortifications=[new(){Id="wall:-1,10",X=-1,Y=10,Damage=42},new(){Id="wall:-1,12",X=-1,Y=12,Gate=true,Open=true,Damage=100}];
        db.Villages.Add(v);await db.SaveChangesAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261017104253_BuildingDisplayNumbers0711");
        await db.Database.ExecuteSqlRawAsync("""UPDATE "Villages" SET "Fortifications"=(SELECT jsonb_agg(w-'Level') FROM jsonb_array_elements("Fortifications") w);""");
        await db.Database.MigrateAsync();db.ChangeTracker.Clear();Assert.False(db.Database.HasPendingModelChanges());
        var restored=await db.Villages.SingleAsync(x=>x.Id==v.Id);Assert.Equal(2,restored.Fortifications.Count);
        var wall=restored.Fortifications[0];Assert.Equal("wall:-1,10",wall.Id);Assert.Equal(-1,wall.X);Assert.Equal(10,wall.Y);Assert.Equal(42,wall.Damage);Assert.Equal(1,wall.Level);
        var gate=restored.Fortifications[1];Assert.True(gate.Gate);Assert.True(gate.Open);Assert.Equal(100,gate.Damage);Assert.Equal(1,gate.Level);Assert.False(gate.Blocks);
    }
}
