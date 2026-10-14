using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Storage;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

/// <summary>Use case: a citizen works, walks about, handles goods and looks after themselves.</summary>
[Collection(ApiCollection.Name)]
public sealed class CitizenWorkTests(DesertodApiFactory factory)
{
    private const string HeapPlot = "17,12";

    private readonly Players players = new(factory);

    private async Task<(FoundedVillage Village, Resident Citizen)> CitizenAsync(string villageName)
    {
        var village = await players.FoundVillageAsync(villageName);
        return (village, await players.JoinAsync(village));
    }

    private Task AddHeapAsync(FoundedVillage village, ResourceKind kind, int amount) =>
        players.ArrangeVillageAsync(village.Slug, (v, now) =>
        {
            v.Storages.Add(StorageBuilding.Create(StorageKind.Heap, HeapPlot, now));
            v.Store(kind, amount);
        });

    private static async Task<JsonElement> MeAsync(Resident citizen) =>
        await OkJsonAsync(await citizen.Client.GetAsync($"{citizen.Path}/me"));

    private static int Carried(JsonElement citizen, string kind) =>
        citizen.GetProperty("load").EnumerateArray()
            .Where(g => g.GetProperty("resource").GetString() == kind)
            .Sum(g => g.GetProperty("amount").GetInt32());

    [Fact]
    public async Task Unknown_task_is_refused()
    {
        var (_, citizen) = await CitizenAsync("Unknown Task");

        var response = await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/task", new { taskId = "juggle-fire" });

        await AssertRuleBrokenAsync(response, "task.unknown");
    }

    [Fact]
    public async Task Citizen_stops_the_running_task()
    {
        var (_, citizen) = await CitizenAsync("Stop Work");
        await OkJsonAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/task", new { taskId = "chop-wood" }));

        var stopped = await OkJsonAsync(await citizen.Client.DeleteAsync($"{citizen.Path}/me/task"));

        Assert.Equal("Idle", stopped.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, stopped.GetProperty("taskId").ValueKind);
        await AssertRuleBrokenAsync(await citizen.Client.DeleteAsync($"{citizen.Path}/me/task"), "task.notRunning");
    }

    [Fact]
    public async Task Working_citizen_cannot_walk_away()
    {
        var (_, citizen) = await CitizenAsync("Busy Walker");
        await OkJsonAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/task", new { taskId = "chop-wood" }));

        var response = await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/goto", new { spot = new { x = 20, y = 20 } });

        await AssertRuleBrokenAsync(response, "goto.working");
    }

    [Fact]
    public async Task Citizen_cannot_walk_beyond_the_land()
    {
        var (_, citizen) = await CitizenAsync("Edge Walker");

        var response = await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/goto", new { spot = new { x = 100000, y = 100000 } });

        await AssertRuleBrokenAsync(response, "spot.outsideLand");
    }

    [Fact]
    public async Task Idle_citizen_takes_goods_out_of_storage()
    {
        var (village, citizen) = await CitizenAsync("Take Goods");
        await AddHeapAsync(village, ResourceKind.Wood, 20);

        var taken = await OkJsonAsync(await citizen.Client.PostAsJsonAsync(
            $"{citizen.Path}/me/take", new { plotId = HeapPlot, resource = "Wood", amount = 4 }));

        Assert.Equal(4, Carried(taken, "Wood"));
        var state = await OkJsonAsync(await village.Tsar.GetAsync(village.Path));
        var wood = state.GetProperty("state").GetProperty("resources").EnumerateArray()
            .Single(r => r.GetProperty("kind").GetString() == "Wood");
        Assert.Equal(16, wood.GetProperty("amount").GetInt32());
    }

    [Fact]
    public async Task Citizen_holds_only_so_much_of_one_good()
    {
        var (village, citizen) = await CitizenAsync("Full Hands");
        await AddHeapAsync(village, ResourceKind.Wood, 30);
        var take = new { plotId = HeapPlot, resource = "Wood", amount = 25 };

        var taken = await OkJsonAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/take", take));

        Assert.Equal(StorageRules.TakeLimit, Carried(taken, "Wood"));
        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/take", take), "take.handsFull");
    }

    [Fact]
    public async Task Nothing_is_taken_from_a_missing_or_empty_storage()
    {
        var (village, citizen) = await CitizenAsync("Empty Heap");
        await AddHeapAsync(village, ResourceKind.Wood, 5);

        await AssertRuleBrokenAsync(
            await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/take", new { plotId = "1,1", resource = "Wood", amount = 1 }),
            "take.noStorage");
        await AssertRuleBrokenAsync(
            await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/take", new { plotId = HeapPlot, resource = "Stone", amount = 1 }),
            "take.nothing");
        await AssertRuleBrokenAsync(
            await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/take", new { plotId = HeapPlot, resource = "Wood", amount = 0 }),
            "take.amount");
    }

    [Fact]
    public async Task Citizen_drops_goods_storage_has_no_room_for()
    {
        var (_, citizen) = await CitizenAsync("No Storage");
        await players.ArrangeCitizenAsync(citizen.Id, (c, _) => c.Carry(ResourceKind.Stone, 3));

        var dropped = await OkJsonAsync(await citizen.Client.PostAsync($"{citizen.Path}/me/drop", null));

        Assert.Empty(dropped.GetProperty("load").EnumerateArray());
        await AssertRuleBrokenAsync(await citizen.Client.PostAsync($"{citizen.Path}/me/drop", null), "hands.empty");
    }

    [Fact]
    public async Task Citizen_keeps_goods_storage_has_room_for()
    {
        var (village, citizen) = await CitizenAsync("Room Left");
        await AddHeapAsync(village, ResourceKind.Wood, 1);
        await players.ArrangeCitizenAsync(citizen.Id, (c, _) => c.Carry(ResourceKind.Wood, 3));

        var response = await citizen.Client.PostAsync($"{citizen.Path}/me/drop", null);

        await AssertRuleBrokenAsync(response, "drop.roomInStorage");
        Assert.Equal(3, Carried(await MeAsync(citizen), "Wood"));
    }

    [Fact]
    public async Task Tired_citizen_drinks_a_vigor_drink()
    {
        var (_, citizen) = await CitizenAsync("Tavern Night");
        await AssertRuleBrokenAsync(await citizen.Client.PostAsync($"{citizen.Path}/me/drink", null), "drink.full");
        await players.ArrangeCitizenAsync(citizen.Id, (c, now) =>
        {
            c.StoredEnergy = 0;
            c.EnergyUpdatedAt = now;
        });
        await AssertRuleBrokenAsync(await citizen.Client.PostAsync($"{citizen.Path}/me/drink", null), "drink.none");
        await players.ArrangeCitizenAsync(citizen.Id, (c, _) => c.Carry(ResourceKind.VigorDrink, 2));

        var refreshed = await OkJsonAsync(await citizen.Client.PostAsync($"{citizen.Path}/me/drink", null));

        Assert.True(refreshed.GetProperty("energy").GetDouble() > 0);
        Assert.Equal(1, Carried(refreshed, "VigorDrink"));
        Assert.Equal(JsonValueKind.String, refreshed.GetProperty("drinkReadyAt").ValueKind);
        await AssertRuleBrokenAsync(await citizen.Client.PostAsync($"{citizen.Path}/me/drink", null), "drink.cooldown");
    }

    [Fact]
    public async Task Wounded_citizen_heals_with_a_remedy()
    {
        var (_, citizen) = await CitizenAsync("Healing Hut");
        await AssertRuleBrokenAsync(await citizen.Client.PostAsync($"{citizen.Path}/me/heal", null), "heal.notInjured");
        await players.ArrangeCitizenAsync(citizen.Id, (c, now) => c.InjuredUntil = now.AddHours(3));
        await AssertRuleBrokenAsync(await citizen.Client.PostAsync($"{citizen.Path}/me/heal", null), "heal.none");
        await players.ArrangeCitizenAsync(citizen.Id, (c, _) => c.Carry(ResourceKind.Remedy, 1));

        var healed = await OkJsonAsync(await citizen.Client.PostAsync($"{citizen.Path}/me/heal", null));

        Assert.Equal(JsonValueKind.Null, healed.GetProperty("injuredUntil").ValueKind);
        Assert.Equal(0, Carried(healed, "Remedy"));
    }
}
