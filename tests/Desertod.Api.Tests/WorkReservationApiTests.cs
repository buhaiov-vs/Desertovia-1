using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain;
using Desertod.Domain.Buildings;
using Desertod.Domain.Rules;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class WorkReservationApiTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    private async Task<FoundedVillage> Prepare()
    {
        var village = await players.FoundVillageAsync("Reserved work");
        await players.ArrangeVillageAsync(village.Slug, (v, now) => {
            v.Trees.Clear();
            var site = ConstructionRules.Place(v, BuildingKind.WoodPile, 9, 11, now);
            site.Record(0, "bring-sticks", site.Required(0, "bring-sticks"), Guid.NewGuid());
            site.Record(0, "stack-pile", site.Required(0, "stack-pile") - 1, Guid.NewGuid());
        });
        return village;
    }

    [Fact]
    public async Task Concurrent_http_starts_claim_one_unit_and_rejections_spend_nothing()
    {
        var village = await Prepare();
        var residents = new[] { await players.JoinAsync(village, "First"), await players.JoinAsync(village, "Second"), await players.JoinAsync(village, "Third") };
        var before = await Task.WhenAll(residents.Select(r => players.LoadCitizenAsync(r.Id)));
        var replies = await Task.WhenAll(residents.Select(r => r.Client.PostAsJsonAsync(village.Path + "/me/job", new { plotId = "9,11", stage = 0, jobId = "stack-pile" })));
        Assert.Single(replies, r => r.IsSuccessStatusCode);
        for (var i = 0; i < replies.Length; i++)
        {
            var saved = await players.LoadCitizenAsync(residents[i].Id);
            if (replies[i].IsSuccessStatusCode) Assert.Equal(1, saved.ReservedSiteWork);
            else
            {
                await AssertRuleBrokenAsync(replies[i], "site.jobBusy");
                Assert.Equal(before[i].StoredEnergy, saved.StoredEnergy);
                Assert.True(saved.CycleSettled);
            }
        }
    }

    [Fact]
    public async Task Independent_service_instances_share_database_admission_lock()
    {
        var village = await Prepare();
        var first = await players.JoinAsync(village, "One");
        var second = await players.JoinAsync(village, "Two");
        var services = new[] { ActivatorUtilities.CreateInstance<GameService>(factory.Services), ActivatorUtilities.CreateInstance<GameService>(factory.Services) };
        var citizens = new[] { await players.LoadCitizenAsync(first.Id), await players.LoadCitizenAsync(second.Id) };
        var outcomes = await Task.WhenAll(services.Select(async (service, i) => {
            try { await service.StartJobAsync(village.Slug, citizens[i].AccountId, new StartJobRequest("9,11", 0, "stack-pile"), default); return "ok"; }
            catch (GameRuleException error) { return error.Code; }
        }));
        Assert.Single(outcomes, result => result == "ok");
        Assert.Single(outcomes, result => result == "site.jobBusy");
    }
}
