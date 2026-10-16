using System.Net.Http.Json;
using Desertod.Domain.Buildings;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;
namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class CivicRequestCapacityTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    [Fact]
    public async Task Concurrent_governor_requests_cannot_overfill_the_last_queue_place()
    {
        var v=await players.FoundVillageAsync("Bounded applications");var planner=await players.JoinAsync(v,"Planner");
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(v.Path+$"/citizens/{planner.Id}/governor",new{isGovernor=true}));
        await using(var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync())
        {
            var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
            foreach(var plot in new[]{"1,1","5,1"})village.Sites.Add(new(){Kind=BuildingKind.LeanTo,PlotId=plot});
            await db.SaveChangesAsync();
        }
        var replies=await Task.WhenAll(planner.Client.PostAsJsonAsync(v.Path+"/blueprints",new{kind="WoodPile",x=9,y=11}),
            planner.Client.PostAsJsonAsync(v.Path+"/blueprints",new{kind="WoodPile",x=21,y=12}));
        Assert.Single(replies,r=>r.IsSuccessStatusCode);
        var denied=Assert.Single(replies,r=>!r.IsSuccessStatusCode);
        await AssertRuleBrokenAsync(denied,"building.requestQueueFull");
        await using var verify=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var current=await verify.Villages.SingleAsync(x=>x.Slug==v.Slug);
        Assert.Single(current.CivicOrders);Assert.Equal(2,current.Sites.Count);
    }
}
