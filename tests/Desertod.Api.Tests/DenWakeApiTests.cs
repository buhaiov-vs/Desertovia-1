using Desertod.Api.Game;
using Desertod.Domain.Catalog;
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
}
