using System.Net;
using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.World;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DenWakeApiTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_PresentCitizen_NearADen_IsAttacked_EvenAfterTheirWorkCycleEnded(bool workedBefore)
    {
        var village = await players.FoundVillageAsync("Den wake");
        var resident = await players.JoinAsync(village);
        var runtime = factory.Services.GetRequiredService<DenRuntime>();
        var villageId = (await players.LoadCitizenAsync(resident.Id)).VillageId;
        var den = runtime.Points(villageId).First(p => p.Level == 1);
        await players.ArrangeCitizenAsync(resident.Id, (citizen, now) =>
        {
            citizen.Trip = null;
            citizen.IdleSpot = new TilePoint(den.Center.X + 3, den.Center.Y);
            if (workedBefore)
            {
                citizen.CurrentTaskId = GameCatalog.Tasks.First().Id;
                citizen.CycleStartedAt = now.AddHours(-2);
                citizen.CycleEndsAt = now.AddHours(-1);
                citizen.CycleSettled = true;
            }
        });
        factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id, village.Slug);

        await Players.EventuallyAsync(() => Task.FromResult(runtime.Fighting(resident.Id) is not null), "the den attacks the citizen");
    }

    [Fact]
    public async Task A_RoamingFoe_AttacksAPresentCitizenNearWhereItWanders()
    {
        var village = await players.FoundVillageAsync("Foe wake");
        var resident = await players.JoinAsync(village);
        var runtime = factory.Services.GetRequiredService<DenRuntime>();
        var villageId = (await players.LoadCitizenAsync(resident.Id)).VillageId;
        var seed = Wilderness.Seed(villageId.ToString());
        var dens = runtime.Points(villageId);
        var foe = Enumerable.Range(15, 60).Select(cell => Haunts.At(seed, cell, 0, dens)).First(foe => foe is not null)!;
        var where = Haunts.PositionAt(seed, foe, factory.Clock.GetUtcNow());
        await players.ArrangeCitizenAsync(resident.Id, (citizen, _) =>
        {
            citizen.Trip = null;
            citizen.IdleSpot = new TilePoint(where.X + 2, where.Y);
        });
        factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id, village.Slug);

        await Players.EventuallyAsync(() => Task.FromResult(runtime.Fighting(resident.Id)?.Id == foe.Id), "the roaming foe attacks the citizen");
    }

    [Fact]
    public async Task ClickingAStandingProtector_StrikesOnlyThatOne()
    {
        var village = await players.FoundVillageAsync("Den click");
        var resident = await players.JoinAsync(village);
        var runtime = factory.Services.GetRequiredService<DenRuntime>();
        var villageId = (await players.LoadCitizenAsync(resident.Id)).VillageId;
        var den = runtime.Points(villageId).First(p => p.Level == 1);
        var home = DenGuards.PatrolAt(den.Center, 2, den.Protectors, den.Level, factory.Clock.GetUtcNow());
        await players.ArrangeCitizenAsync(resident.Id, (citizen, _) =>
        {
            citizen.Trip = null;
            citizen.IdleSpot = home;
        });

        var response = await resident.Client.PostAsJsonAsync($"{village.Path}/dens/{den.Id}/attack", new { protector = 2 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await Players.EventuallyAsync(() =>
        {
            var live = runtime.Fighting(resident.Id);
            if (live is null)
            {
                return Task.FromResult(false);
            }

            lock (live.Gate)
            {
                var fight = live.Fight;
                return Task.FromResult(fight.Hero?.Target == 2
                    && fight.Protectors[2].Health < fight.Protectors[2].Guard.Health
                    && fight.Protectors.Where(p => p.Index != 2).All(p => p.Health == p.Guard.Health));
            }
        }, "only the chosen protector is hurt");
    }

    [Fact]
    public async Task WalkingIntoADen_DealsNoDamage()
    {
        var village = await players.FoundVillageAsync("Den run");
        var resident = await players.JoinAsync(village);
        var runtime = factory.Services.GetRequiredService<DenRuntime>();
        var villageId = (await players.LoadCitizenAsync(resident.Id)).VillageId;
        var den = runtime.Points(villageId).First(p => p.Level == 1);
        var home = DenGuards.PatrolAt(den.Center, 0, den.Protectors, den.Level, factory.Clock.GetUtcNow());
        await players.ArrangeCitizenAsync(resident.Id, (citizen, _) =>
        {
            citizen.Trip = null;
            citizen.IdleSpot = home;
        });
        factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id, village.Slug);

        await Players.EventuallyAsync(() => Task.FromResult(runtime.Fighting(resident.Id) is not null), "the den attacks the citizen");
        await Task.Delay(TimeSpan.FromSeconds(2));
        var live = runtime.Fighting(resident.Id)!;
        lock (live.Gate)
        {
            Assert.Null(live.Fight.Hero!.Target);
            Assert.All(live.Fight.Protectors, p => Assert.Equal(p.Guard.Health, p.Health));
            Assert.True(live.Fight.Hero.Health < live.Fight.Hero.Fighter.Health);
        }
    }

    [Fact]
    public async Task ClickingAFarProtector_WalksThere_AndALaterWalkCancelsTheAttack()
    {
        var village = await players.FoundVillageAsync("Den walk");
        var resident = await players.JoinAsync(village);
        var runtime = factory.Services.GetRequiredService<DenRuntime>();
        var villageId = (await players.LoadCitizenAsync(resident.Id)).VillageId;
        var den = runtime.Points(villageId).First(p => p.Level == 1);
        var camp = WorldMap.Places[WorldMap.Camp];

        var attack = await resident.Client.PostAsJsonAsync($"{village.Path}/dens/{den.Id}/attack", new { protector = 0 });
        Assert.Equal(HttpStatusCode.NoContent, attack.StatusCode);
        var walking = await players.LoadCitizenAsync(resident.Id);
        Assert.NotNull(walking.Trip);
        Assert.InRange(walking.Trip.To.DistanceTo(den.Center), DenGuards.PostRadius(den.Level) - 0.5, DenGuards.PostRadius(den.Level) + 0.5);
        Assert.Null(runtime.Fighting(resident.Id));

        var away = await resident.Client.PostAsJsonAsync($"{village.Path}/me/goto", new { spot = new { x = camp.X, y = camp.Y } });
        Assert.Equal(HttpStatusCode.OK, away.StatusCode);
        var home = DenGuards.PatrolAt(den.Center, 0, den.Protectors, den.Level, factory.Clock.GetUtcNow());
        await players.ArrangeCitizenAsync(resident.Id, (citizen, _) =>
        {
            citizen.Trip = null;
            citizen.IdleSpot = home;
        });
        factory.Services.GetRequiredService<CitizenPresence>().Touch(resident.Id, village.Slug);
        await Players.EventuallyAsync(() => Task.FromResult(runtime.Fighting(resident.Id) is not null), "the den attacks the citizen");
        await Task.Delay(TimeSpan.FromSeconds(2));
        var live = runtime.Fighting(resident.Id)!;
        lock (live.Gate)
        {
            Assert.Null(live.Fight.Hero!.Target);
            Assert.All(live.Fight.Protectors, p => Assert.Equal(p.Guard.Health, p.Health));
        }
    }
}
