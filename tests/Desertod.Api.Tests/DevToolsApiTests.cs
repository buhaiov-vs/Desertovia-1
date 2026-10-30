using System.Net.Http.Json;
using Desertod.Domain.Combat;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DevToolsApiTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    [Fact]
    public async Task Any_player_spawns_every_monster_soon_and_cancels_it()
    {
        var v = await players.FoundVillageAsync("Dev monsters");
        var helper = await players.JoinAsync(v);
        await OkJsonAsync(await helper.Client.PostAsJsonAsync(v.Path + "/dev/raid", new { everyMonster = true }));
        await AssertRuleBrokenAsync(await helper.Client.PostAsJsonAsync(v.Path + "/dev/raid", new { everyMonster = false }), "raid.pending");
        Guid villageId;
        await using (var db = await Db())
        {
            villageId = (await db.Villages.AsNoTracking().SingleAsync(x => x.Slug == v.Slug)).Id;
            var battle = await db.RaidBattles.AsNoTracking().SingleAsync(b => b.VillageId == villageId);
            Assert.InRange((battle.StartsAt - factory.Clock.GetUtcNow()).TotalSeconds, 1, 5);
            Assert.Equal(EnemyCatalog.Enemies.Count * 2, battle.Units.Count);
            Assert.Equal(EnemyCatalog.Enemies.Select(e => (EnemyKind?)e.Kind).Order(), battle.Units.Select(u => u.Enemy).Distinct().Order());
            Assert.All(battle.Units, u => Assert.Equal(u.MaximumHealth, u.Health));
        }
        await OkJsonAsync(await helper.Client.DeleteAsync(v.Path + "/dev/raid"));
        await using (var db = await Db())
        {
            var battle = await db.RaidBattles.AsNoTracking().SingleAsync(b => b.VillageId == villageId);
            Assert.NotNull(battle.FinishedAt);
        }
        await OkJsonAsync(await helper.Client.PostAsJsonAsync(v.Path + "/dev/raid", new { everyMonster = false }));
    }

    [Fact]
    public async Task Night_with_attack_sends_the_dens_in_a_new_village_by_day()
    {
        var v = await players.FoundVillageAsync("Dev night");
        var helper = await players.JoinAsync(v);
        await OkJsonAsync(await helper.Client.PostAsync(v.Path + "/dev/night-raid", null));
        await AssertRuleBrokenAsync(await helper.Client.PostAsync(v.Path + "/dev/night-raid", null), "raid.pending");
        await using (var db = await Db())
        {
            var village = await db.Villages.AsNoTracking().Include(x => x.Raids).SingleAsync(x => x.Slug == v.Slug);
            var raids = village.Raids.Where(r => r.IsPending).ToList();
            Assert.NotEmpty(raids);
            Assert.All(raids, r => Assert.Equal(RaidSource.Night, r.Source));
            // Nothing is explored yet, so the lowest dens that may raid a village send: wolves; slimes have no server foe, rats and imps stay underground.
            Assert.All(raids, r => Assert.Equal(EnemyKind.Wolf, r.Enemy));
            Assert.All(raids, r => Assert.InRange((r.StrikesAt - factory.Clock.GetUtcNow()).TotalSeconds, 1, 5));
            Assert.Single(await db.RaidBattles.AsNoTracking().Where(b => b.VillageId == village.Id && b.FinishedAt == null).ToListAsync());
        }
        await OkJsonAsync(await helper.Client.DeleteAsync(v.Path + "/dev/raid"));
    }
}
