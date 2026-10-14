using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Combat;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

/// <summary>
/// Use case: time passes in the village. Work ends and is banked, whether or not the player is looking, and the undead
/// strike when they said they would. The test clock jumps forward instead of waiting.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TimePassingTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    private static async Task<JsonElement> StartAsync(Resident citizen, string taskId = "chop-wood") =>
        await OkJsonAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/task", new { taskId }));

    /// <summary>Moves the clock to just after the running cycle ends, inside the grace window to renew it.</summary>
    private void FinishCycle(JsonElement citizen)
    {
        var left = citizen.GetProperty("cycleEndsAt").GetDateTimeOffset() - factory.Clock.GetUtcNow();
        factory.Clock.Advance((left > TimeSpan.Zero ? left : TimeSpan.Zero) + TimeSpan.FromSeconds(1));
    }

    private static int Experience(JsonElement citizen) =>
        citizen.GetProperty("stats").EnumerateArray().Sum(s => s.GetProperty("experience").GetInt32() + s.GetProperty("level").GetInt32());

    [Fact]
    public async Task Finished_work_is_banked_when_the_player_comes_back()
    {
        var village = await players.FoundVillageAsync("Banked Work");
        var citizen = await players.JoinAsync(village);
        var started = await StartAsync(citizen);
        Assert.Equal(0, Experience(started));

        FinishCycle(started);
        var back = await OkJsonAsync(await citizen.Client.GetAsync($"{citizen.Path}/me"));

        Assert.Equal("AwaitingRenewal", back.GetProperty("state").GetString());
        Assert.True(Experience(back) > 0);
        Assert.True(back.GetProperty("energy").GetDouble() < back.GetProperty("maximumEnergy").GetDouble());
        // The new village has no storage yet, so the wood stays in hands.
        Assert.Contains(back.GetProperty("load").EnumerateArray(), good => good.GetProperty("resource").GetString() == "Wood");
    }

    [Fact]
    public async Task Renewing_work_inside_the_grace_window_builds_a_streak()
    {
        var village = await players.FoundVillageAsync("Streak Village");
        var citizen = await players.JoinAsync(village);
        // Without storage the wood would pile up in hands until they are full.
        await players.ArrangeVillageAsync(village.Slug, (v, now) => v.Storages.Add(StorageBuilding.Create(StorageKind.Heap, "17,12", now)));
        var first = await StartAsync(citizen);
        Assert.Equal(0, first.GetProperty("streak").GetInt32());

        FinishCycle(first);
        var renewed = await StartAsync(citizen);

        Assert.Equal("Working", renewed.GetProperty("state").GetString());
        Assert.Equal(1, renewed.GetProperty("streak").GetInt32());
        Assert.True(Experience(renewed) > 0);
    }

    [Fact]
    public async Task Work_is_banked_while_nobody_is_looking()
    {
        var village = await players.FoundVillageAsync("Unattended Work");
        var citizen = await players.JoinAsync(village);
        var started = await StartAsync(citizen);

        FinishCycle(started);

        await EventuallyAsync(async () => (await players.LoadCitizenAsync(citizen.Id)).CycleSettled, "the worker banks the cycle");
        var banked = await players.LoadCitizenAsync(citizen.Id);
        Assert.True(Enum.GetValues<Stat>().Sum(stat => banked.Stats.GetExperience(stat) + banked.Stats.Get(stat)) > 0, "The cycle earned nothing.");
    }

    [Fact]
    public async Task Undead_strike_the_camp_when_announced_even_unwatched()
    {
        var village = await players.FoundVillageAsync("Night Of The Dead");
        await players.JoinAsync(village);
        var called = await OkJsonAsync(await village.Tsar.PostAsJsonAsync($"{village.Path}/undead", new { enemy = "Zombie", size = "Small" }));
        var strikesAt = called.GetProperty("raids")[0].GetProperty("strikesAt").GetDateTimeOffset();

        factory.Clock.Advance(strikesAt - factory.Clock.GetUtcNow() + TimeSpan.FromSeconds(1));

        JsonElement raid = default;
        await EventuallyAsync(async () =>
        {
            var state = await OkJsonAsync(await village.Tsar.GetAsync(village.Path));
            raid = state.GetProperty("state").GetProperty("raids")[0];
            return raid.GetProperty("outcome").GetString() != nameof(RaidOutcome.Pending);
        }, "the raid worker fights the undead");
        Assert.Equal(JsonValueKind.String, raid.GetProperty("resolvedAt").ValueKind);
        Assert.Contains(raid.GetProperty("outcome").GetString(), new[] { nameof(RaidOutcome.Repelled), nameof(RaidOutcome.Lost) });
    }

    [Fact]
    public async Task Citizen_works_on_a_planned_building_and_the_plan_can_no_longer_be_withdrawn()
    {
        var village = await players.FoundVillageAsync("Builders Village");
        var citizen = await players.JoinAsync(village, "Builder");
        await OkJsonAsync(await village.Tsar.PostAsJsonAsync($"{village.Path}/blueprints", new { kind = "WoodPile", x = 9, y = 11 }));
        var job = $"{citizen.Path}/me/job";
        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync(job, new { plotId = "9,11", stage = 0, jobId = "dance" }), "site.unknownJob");
        await AssertRuleBrokenAsync(
            await citizen.Client.PostAsJsonAsync(job, new { plotId = "9,11", stage = 0, jobId = "bring-sticks" }), "site.noMaterial");

        var working = await OkJsonAsync(await citizen.Client.PostAsJsonAsync(job, new { plotId = "9,11", stage = 0, jobId = "clear-ground" }));
        Assert.Equal("9,11", working.GetProperty("siteId").GetString());
        Assert.Equal(0, working.GetProperty("siteStage").GetInt32());

        FinishCycle(working);
        await OkJsonAsync(await citizen.Client.GetAsync($"{citizen.Path}/me"));

        var state = await OkJsonAsync(await village.Tsar.GetAsync(village.Path));
        var site = Assert.Single(state.GetProperty("state").GetProperty("sites").EnumerateArray());
        Assert.True(site.GetProperty("progress").GetDouble() > 0);
        await AssertRuleBrokenAsync(await village.Tsar.DeleteAsync($"{village.Path}/blueprints/9,11"), "building.cannotCancel");
    }
}
