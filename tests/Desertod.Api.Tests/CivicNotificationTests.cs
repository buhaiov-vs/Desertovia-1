using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Desertod.Api.Game;
using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;
namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class CivicNotificationTests(DesertodApiFactory factory)
{
    private readonly Players players = new(factory);
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    [Fact]
    public async Task Existing_pending_request_is_claimed_once_and_failed_bridge_retries_without_changing_building()
    {
        var v = await players.FoundVillageAsync("Civic telegram queue");
        var planner = await players.JoinAsync(v, "Planner");
        Guid villageId, orderId;
        await using (var db = await Db())
        {
            var village = await db.Villages.SingleAsync(x => x.Slug == v.Slug); villageId = village.Id;
            var actor = (await db.Citizens.SingleAsync(c => c.Id == planner.Id)).AccountId;
            var order = new CivicOrder { PlotId="9,11", Operation="build", BuildingKind=BuildingKind.Hut, RequestedBy=actor, CreatedAt=factory.Clock.GetUtcNow() };
            village.CivicOrders.Add(order); orderId=order.Id;
            db.ExternalLogins.Add(new(){Provider="telegram",ProviderUserId="71073101",AccountId=village.OwnerAccountId});
            await db.SaveChangesAsync();
        }
        var game=factory.Services.GetRequiredService<GameService>();
        var claims=await Task.WhenAll(game.ClaimCivicNoticeAsync(villageId,default),game.ClaimCivicNoticeAsync(villageId,default));
        var notice=Assert.Single(claims,n=>n is not null)!;
        Assert.Equal(orderId,notice.OrderId);Assert.Equal("71073101",notice.OwnerId);Assert.Equal("Hut",notice.BuildingKind);
        await game.CompleteCivicNoticeAsync(villageId,orderId,"Unavailable",default);
        Assert.Null(await game.ClaimCivicNoticeAsync(villageId,default));
        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(orderId,(await game.ClaimCivicNoticeAsync(villageId,default))!.OrderId);
        await game.CompleteCivicNoticeAsync(villageId,orderId,"Sent",default);
        factory.Clock.Advance(TimeSpan.FromMinutes(5));Assert.Null(await game.ClaimCivicNoticeAsync(villageId,default));
        await using var reloaded=await Db();var current=await reloaded.Villages.SingleAsync(x=>x.Id==villageId);
        Assert.Empty(current.Sites);Assert.Equal("pending",Assert.Single(current.CivicOrders).Status);
        Assert.Equal("Sent",Assert.Single(current.CivicOrders).NoticeState);
    }

    [Fact]
    public async Task Signed_owner_vote_revalidates_and_is_idempotent_other_accounts_cannot_approve()
    {
        var v=await players.FoundVillageAsync("Civic telegram vote");var planner=await players.JoinAsync(v,"Planner");
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(v.Path+$"/citizens/{planner.Id}/governor",new{isGovernor=true}));
        var requested=await OkJsonAsync(await planner.Client.PostAsJsonAsync(v.Path+"/blueprints",new{kind="WoodPile",x=9,y=11}));
        var oid=requested.GetProperty("civicOrders")[0].GetProperty("id").GetGuid();
        await using(var db=await Db())
        {
            var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
            db.ExternalLogins.Add(new(){Provider="telegram",ProviderUserId="71073102",AccountId=village.OwnerAccountId});await db.SaveChangesAsync();
        }
        await using var configured=factory.WithWebHostBuilder(b=>b.UseSetting("Authentication:Telegram:BotUsername","test_bot").UseSetting("Hosting:PrimaryVillage",v.Slug));
        using var client=configured.CreateClient();
        object Vote(string owner,bool approve=true)=>new{orderId=oid,slug=v.Slug,ownerId=owner,approve};
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/civic/vote",Vote("71073102"))).StatusCode);
        client.DefaultRequestHeaders.Add("X-Desertovia-Bridge",Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("desertovia-bridge:"+DesertodApiFactory.TelegramBotToken))));
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/civic/vote",Vote("71073103"))).StatusCode);
        for(var i=0;i<2;i++)Assert.True((await client.PostAsJsonAsync("/api/civic/vote",Vote("71073102"))).IsSuccessStatusCode);
        Assert.True((await client.PostAsJsonAsync("/api/civic/vote",Vote("71073102",false))).IsSuccessStatusCode);
        await using var verify=await Db();var current=await verify.Villages.SingleAsync(x=>x.Slug==v.Slug);
        Assert.Single(current.Sites);Assert.Equal("working",Assert.Single(current.CivicOrders).Status);
    }
}
