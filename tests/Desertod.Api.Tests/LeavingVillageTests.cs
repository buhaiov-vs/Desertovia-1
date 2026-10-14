using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Catalog;
using Desertod.Domain.Storage;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

/// <summary>Use case: a citizen leaves the village for good, and the Tsar deletes their village.</summary>
[Collection(ApiCollection.Name)]
public sealed class LeavingVillageTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);

    [Fact]
    public async Task Citizen_who_leaves_stops_work_leaves_goods_in_storage_and_may_come_back_fresh()
    {
        var village = await players.FoundVillageAsync("Left Behind");
        var citizen = await players.JoinAsync(village, "Leaver");
        await players.ArrangeVillageAsync(village.Slug, (v, now) => v.Storages.Add(StorageBuilding.Create(StorageKind.Heap, "17,12", now)));
        Assert.Equal(HttpStatusCode.OK, (await citizen.Client.PostAsJsonAsync($"{village.Path}/me/task", new { taskId = "chop-wood" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await citizen.Client.PostAsJsonAsync($"{village.Path}/chat", new { text = "Farewell" })).StatusCode);
        await players.ArrangeCitizenAsync(citizen.Id, (c, _) => c.Carry(ResourceKind.Wood, 3));
        var woodBefore = await StockAsync(village, ResourceKind.Wood);

        Assert.Equal(HttpStatusCode.NoContent, (await citizen.Client.DeleteAsync($"{village.Path}/me")).StatusCode);

        Assert.Equal(woodBefore + 3, await StockAsync(village, ResourceKind.Wood));
        var seen = await OkJsonAsync(await village.Tsar.GetAsync(village.Path));
        Assert.Empty(seen.GetProperty("citizens").EnumerateArray());
        var chat = await OkJsonAsync(await village.Tsar.GetAsync($"{village.Path}/chat"));
        Assert.Empty(chat.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await citizen.Client.GetAsync($"{village.Path}/me")).StatusCode);

        var back = await OkJsonAsync(await citizen.Client.PostAsJsonAsync($"{village.Path}/citizens", new { }));
        Assert.NotEqual(citizen.Id, back.GetProperty("id").GetGuid());
        Assert.Equal(0, back.GetProperty("renown").GetInt32());
    }

    [Fact]
    public async Task Outsider_cannot_leave_a_village_they_do_not_live_in()
    {
        var village = await players.FoundVillageAsync("Stay Out");
        var outsider = await players.WithCharacterAsync("Outsider");

        await AssertRejectedAsync(await outsider.Client.DeleteAsync($"{village.Path}/me"), HttpStatusCode.NotFound, "citizen.notInVillage");
    }

    [Fact]
    public async Task Tsar_deletes_the_village_and_its_citizens_lose_it()
    {
        var village = await players.FoundVillageAsync("Short Lived");
        var citizen = await players.JoinAsync(village);

        Assert.Equal(HttpStatusCode.NoContent, (await village.Tsar.DeleteAsync(village.Path)).StatusCode);

        await AssertRejectedAsync(await village.Tsar.GetAsync(village.Path), HttpStatusCode.NotFound, "village.notFound");
        var mine = await citizen.Client.GetFromJsonAsync<JsonElement>("/api/villages");
        Assert.DoesNotContain(mine.EnumerateArray(), v => v.GetProperty("slug").GetString() == village.Slug);
    }

    [Fact]
    public async Task Only_the_Tsar_may_delete_the_village()
    {
        var village = await players.FoundVillageAsync("Kept Village");
        var citizen = await players.JoinAsync(village);

        await AssertForbiddenAsync(await citizen.Client.DeleteAsync(village.Path));
        Assert.Equal(HttpStatusCode.OK, (await village.Tsar.GetAsync(village.Path)).StatusCode);
    }

    private async Task<int> StockAsync(FoundedVillage village, ResourceKind kind)
    {
        var stock = 0;
        await players.ArrangeVillageAsync(village.Slug, (v, _) => stock = v.Stock(kind));
        return stock;
    }
}
