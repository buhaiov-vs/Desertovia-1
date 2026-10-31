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

    [Fact]
    public async Task Resurrect_stands_fallen_heroes_up_and_leaves_exhaustion()
    {
        var v = await players.FoundVillageAsync("Dev resurrect");
        var fallen = await players.JoinAsync(v, "Fallen");
        var tired = await players.JoinAsync(v, "Tired");
        var cycleEnds = factory.Clock.GetUtcNow().AddMinutes(40);
        await players.ArrangeCitizenAsync(fallen.Id, (c, now) =>
        {
            c.InjuredUntil = now.AddHours(2);
            c.WoundedAt = now;
            c.HospitalizedAt = now;
            c.RescueRewardEligible = true;
            c.RecoveryPausedAt = now.AddMinutes(-10);
            c.CycleSettled = false;
            c.CycleStartedAt = now.AddMinutes(-20);
            c.CycleEndsAt = cycleEnds;
        });
        var exhaustedUntil = factory.Clock.GetUtcNow().AddHours(1);
        await players.ArrangeCitizenAsync(tired.Id, (c, now) => c.ExhaustedUntil = exhaustedUntil);
        await players.ArrangeVillageAsync(v.Slug, (village, now) =>
        {
            village.TsarInjuredUntil = now.AddMinutes(30);
            village.TsarWoundedAt = now;
            village.TsarHospitalizedAt = now;
            village.TsarRescueRewardEligible = true;
        });
        await OkJsonAsync(await fallen.Client.PostAsJsonAsync(v.Path + "/dev/raid", new { everyMonster = false }));
        Guid battleId;
        await using (var db = await Db())
        {
            var villageId = (await db.Villages.AsNoTracking().SingleAsync(x => x.Slug == v.Slug)).Id;
            var battle = await db.RaidBattles.SingleAsync(b => b.VillageId == villageId && b.FinishedAt == null);
            battleId = battle.Id;
            battle.Fighters.Add(new RaidFighter
            {
                CitizenId = fallen.Id, Health = 0, MaximumHealth = 48, Wounded = true, Active = false,
            });
            battle.Fighters.Add(new RaidFighter
            {
                CitizenId = tired.Id, Health = 20, MaximumHealth = 40, Wounded = false, Active = true,
            });
            await db.SaveChangesAsync();
        }

        await OkJsonAsync(await fallen.Client.PostAsync(v.Path + "/dev/resurrect", null));

        var savedFallen = await players.LoadCitizenAsync(fallen.Id);
        Assert.Null(savedFallen.InjuredUntil);
        Assert.Null(savedFallen.WoundedAt);
        Assert.Null(savedFallen.HospitalizedAt);
        Assert.False(savedFallen.RescueRewardEligible);
        Assert.Null(savedFallen.RecoveryPausedAt);
        Assert.True(savedFallen.CycleEndsAt >= cycleEnds.AddMinutes(10));
        var savedTired = await players.LoadCitizenAsync(tired.Id);
        Assert.NotNull(savedTired.ExhaustedUntil);
        Assert.True(Math.Abs((savedTired.ExhaustedUntil.Value - exhaustedUntil).TotalMilliseconds) < 1);
        Assert.Null(savedTired.InjuredUntil);
        await using (var db = await Db())
        {
            var village = await db.Villages.AsNoTracking().SingleAsync(x => x.Slug == v.Slug);
            Assert.Null(village.TsarInjuredUntil);
            Assert.Null(village.TsarWoundedAt);
            Assert.Null(village.TsarHospitalizedAt);
            Assert.False(village.TsarRescueRewardEligible);
            var battle = await db.RaidBattles.AsNoTracking().SingleAsync(b => b.Id == battleId);
            var down = Assert.Single(battle.Fighters, f => f.CitizenId == fallen.Id);
            Assert.Equal(48, down.Health);
            Assert.False(down.Wounded);
            Assert.True(down.Active);
            var fine = Assert.Single(battle.Fighters, f => f.CitizenId == tired.Id);
            Assert.Equal(20, fine.Health);
            Assert.True(fine.Active);
            Assert.False(fine.Wounded);
        }
    }
}
