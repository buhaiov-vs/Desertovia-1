using System.Net;
using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Economy;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class KingdomEconomyApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db() => factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    private static async Task<EconomyView> View(HttpClient client,string path)
    {
        var response=await client.GetAsync(path+"/economy");
        await Players.OkJsonAsync(response);
        return (await response.Content.ReadFromJsonAsync<EconomyView>(JsonOptions))!;
    }
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions=new(System.Text.Json.JsonSerializerDefaults.Web)
    { Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()} };

    private async Task SeedWallet(string slug,Guid citizenId,long coins)
    {
        await using var db=await Db();var c=await db.Citizens.SingleAsync(x=>x.Id==citizenId);
        var wallet=await db.GameWallets.SingleAsync(x=>x.VillageId==c.VillageId&&x.AccountId==c.AccountId);
        wallet.Coins=coins;await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Common_purchase_is_once_only_and_a_reused_id_cannot_change_the_amount()
    {
        var v=await players.FoundVillageAsync("Stock purchase");var a=await players.JoinAsync(v,"Buyer");
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{var s=StorageBuilding.Create(StorageKind.TownHall,"3,3",now);s.Put(ResourceKind.Wood,10);city.Storages.Add(s);});
        await View(a.Client,v.Path);await SeedWallet(v.Slug,a.Id,1000);
        var request=new{requestId=Guid.NewGuid(),action="buy-stock",resource="Wood",amount=3,price=2};
        var replies=await Task.WhenAll(a.Client.PostAsJsonAsync(v.Path+"/economy",request),a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        foreach(var reply in replies)await Players.OkJsonAsync(reply);
        var after=await View(a.Client,v.Path);Assert.Equal(994,after.Coins);Assert.Equal(6,after.Treasury);Assert.Equal(3,Assert.Single(after.Inventory).Amount);
        var bad=await a.Client.PostAsJsonAsync(v.Path+"/economy",new{request.requestId,action="buy-stock",resource="Wood",amount=4,price=2});
        await Players.AssertRuleBrokenAsync(bad,"economy.reusedRequest");
        await using var db=await Db();var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);Assert.Equal(7,city.Stock(ResourceKind.Wood));
        Assert.Equal(1,await db.EconomyReceipts.CountAsync(x=>x.VillageId==city.Id&&x.RequestId==request.requestId));
        Assert.Contains(after.Journal,x=>x.Asset=="Wood"&&x.Delta==-3);Assert.Contains(after.Journal,x=>x.Asset=="Wood"&&x.Delta==3);
    }

    [Fact]
    public async Task Two_buyers_cannot_buy_the_same_lot_and_money_and_basis_are_conserved()
    {
        var v=await players.FoundVillageAsync("Market race");var a=await players.JoinAsync(v,"Seller");var b=await players.JoinAsync(v,"First");var c=await players.JoinAsync(v,"Second");
        foreach(var resident in new[]{a,b,c}){await View(resident.Client,v.Path);await SeedWallet(v.Slug,resident.Id,1000);}
        await players.ArrangeCitizenAsync(a.Id,(p,_)=>PersonalInventory.Add(p,ResourceKind.Planks,4,20));
        var lotId=Guid.NewGuid();await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=lotId,action="list",resource="Planks",amount=4,price=80}));
        var rb=new{requestId=Guid.NewGuid(),action="buy-lot",target=lotId};var rc=new{requestId=Guid.NewGuid(),action="buy-lot",target=lotId};
        var replies=await Task.WhenAll(b.Client.PostAsJsonAsync(v.Path+"/economy",rb),c.Client.PostAsJsonAsync(v.Path+"/economy",rc));
        Assert.Single(replies,x=>x.IsSuccessStatusCode);Assert.Single(replies,x=>x.StatusCode==HttpStatusCode.BadRequest);
        var winner=replies[0].IsSuccessStatusCode?b:c;
        if(replies[0].IsSuccessStatusCode)await Players.OkJsonAsync(await winner.Client.PostAsJsonAsync(v.Path+"/economy",rb));
        else await Players.OkJsonAsync(await winner.Client.PostAsJsonAsync(v.Path+"/economy",rc));
        var seller=await View(a.Client,v.Path);var first=await View(b.Client,v.Path);var second=await View(c.Client,v.Path);
        Assert.Equal(3000,seller.Coins+first.Coins+second.Coins);Assert.Equal(1080,seller.Coins);Assert.Equal(60,seller.ProfitToday);
        Assert.Equal(4,first.Inventory.Sum(x=>x.Amount)+second.Inventory.Sum(x=>x.Amount));
        Assert.Equal(80,first.Inventory.Sum(x=>x.CostBasis)+second.Inventory.Sum(x=>x.CostBasis));
        Assert.False(seller.TaxEnabled);Assert.Equal(0,seller.TaxPreview);
    }

    [Fact]
    public async Task Exchange_is_test_only_daily_limited_and_idempotent()
    {
        var v=await players.FoundVillageAsync("Test exchange");var a=await players.JoinAsync(v,"Saver");
        await View(a.Client,v.Path);await SeedWallet(v.Slug,a.Id,2000);
        var request=new{requestId=Guid.NewGuid(),action="exchange",amount=5};
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        var view=await View(a.Client,v.Path);Assert.Equal(1500,view.Coins);Assert.Equal(5,view.TestCandles);Assert.Equal(5,view.ExchangedToday);Assert.Equal(45,view.KingdomCandleRoom);
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="exchange",amount=1}),"exchange.limit");
        await using var db=await Db();var person=await db.Citizens.SingleAsync(x=>x.Id==a.Id);
        var wallet=await db.GameWallets.SingleAsync(x=>x.VillageId==person.VillageId&&x.AccountId==person.AccountId);wallet.ExchangeDay=DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime.AddDays(-1));
        await db.SaveChangesAsync();
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="exchange",amount=1}));
        Assert.Equal(1,(await View(a.Client,v.Path)).ExchangedToday);
    }

    [Fact]
    public async Task Useful_cycle_receives_one_wage_and_repeat_settlement_does_not_pay_again()
    {
        var v=await players.FoundVillageAsync("Useful wage");var a=await players.JoinAsync(v,"Worker");
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>city.Storages.Add(StorageBuilding.Create(StorageKind.TownHall,"3,3",now)));
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="chop-wood"}));
        await players.ArrangeCitizenAsync(a.Id,(p,now)=>{p.CycleStartedAt=now.AddMinutes(-5);p.CycleEndsAt=now.AddSeconds(-1);});
        var game=factory.Services.GetRequiredService<GameService>();await using var db=await Db();var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        await game.SettleVillageAsync(city.Id,[a.Id],default);await game.SettleVillageAsync(city.Id,[a.Id],default);
        var view=await View(a.Client,v.Path);Assert.Equal(view.WorkPay,view.Coins);Assert.Single(view.Journal,x=>x.Operation=="wage");
        Assert.Single((await View(v.Tsar,v.Path)).Journal,x=>x.Operation=="wage");
        var before=(await players.LoadCitizenAsync(a.Id)).Skills.Sum(x=>x.Experience);
        await game.SettleVillageAsync(city.Id,[a.Id],default);Assert.Equal(before,(await players.LoadCitizenAsync(a.Id)).Skills.Sum(x=>x.Experience));
    }

    [Fact]
    public async Task Primary_world_service_and_http_delete_preserve_progress_while_logout_still_works()
    {
        var v=await players.FoundVillageAsync("Protected realm");var a=await players.JoinAsync(v,"Resident");
        await players.ArrangeCitizenAsync(a.Id,(p,_)=>p.Renown=73);
        await using var db=await Db();var person=await db.Citizens.AsNoTracking().SingleAsync(x=>x.Id==a.Id);
        var login=await db.ExternalLogins.Where(x=>x.AccountId==person.AccountId).Select(x=>x.ProviderUserId).FirstAsync();
        await using var mounted=factory.WithWebHostBuilder(builder=>builder.UseSetting("Hosting:PrimaryVillage",v.Slug));
        var service=mounted.Services.GetRequiredService<GameService>();
        await Assert.ThrowsAsync<Desertod.Domain.GameRuleException>(()=>service.LeaveVillageAsync(v.Slug,person.AccountId,default));
        await Assert.ThrowsAsync<Desertod.Domain.GameRuleException>(()=>service.DeleteVillageAsync(v.Slug,person.AccountId,default));
        using var client=mounted.CreateClient();await client.PostAsJsonAsync("/auth/dev-login",new{name=login});
        await Players.AssertRuleBrokenAsync(await client.DeleteAsync(v.Path+"/me"),"village.protectedWorld");
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsync("/auth/logout",null)).StatusCode);
        var saved=await players.LoadCitizenAsync(a.Id);Assert.Equal(person.Id,saved.Id);Assert.Equal(73,saved.Renown);
        Assert.True(await db.Villages.AnyAsync(x=>x.Slug==v.Slug));
    }

    [Fact]
    public async Task Directed_raid_path_and_guard_deployment_survive_real_database_reload()
    {
        var v=await players.FoundVillageAsync("Directed raid");var residents=new[]{await players.JoinAsync(v,"Guard"),await players.JoinAsync(v,"One"),await players.JoinAsync(v,"Two")};
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{city.CreatedAt=now.AddDays(-10);city.RaidsPlannedPeriod=long.MaxValue;});
        var guardTask=GameCatalog.Tasks.First(t=>t.Effect==TaskEffect.Guard);
        await players.ArrangeCitizenAsync(residents[0].Id,(p,now)=>{p.CurrentTaskId=guardTask.Id;p.CycleSettled=false;p.CycleStartedAt=now;p.CycleEndsAt=now.AddHours(1);});
        foreach(var p in residents)factory.Services.GetRequiredService<CitizenPresence>().Touch(p.Id,v.Slug);
        await using var db=await Db();var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug);var service=factory.Services.GetRequiredService<GameService>();
        var review=await service.BeginAlmightyReviewAsync(city.Id,default);Assert.NotNull(review);
        var option=review.Options.First(x=>x.Enemy==EnemyKind.Skeleton&&x.Sector is not null);
        await service.CompleteAlmightyReviewAsync(review,new(option.Id,"Проверенный подход","Не обходить оборону"),default);
        var proposal=await db.AlmightyProposals.AsNoTracking().SingleAsync(x=>x.VillageId==city.Id);
        await Players.OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true}));
        await Players.OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+$"/director/{proposal.Id}/vote",new{accept=true}));
        var loaded=await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==city.Id);var raid=Assert.Single(loaded.Raids);
        Assert.Equal(option.Sector,raid.EntrySector);Assert.True(raid.ApproachPath.Count>1);
        Assert.All(raid.ApproachPath.Zip(raid.ApproachPath.Skip(1)),pair=>Assert.Equal(1,Math.Abs(pair.First.X-pair.Second.X)+Math.Abs(pair.First.Y-pair.Second.Y)));
        Assert.True((await residents[0].Client.PutAsJsonAsync(v.Path+"/me/defense",new{mode=option.Sector})).IsSuccessStatusCode);
        var guard=await players.LoadCitizenAsync(residents[0].Id);Assert.Equal(option.Sector,guard.DefenseSector);Assert.NotNull(guard.DefenseReadyAt);
        Assert.InRange((guard.DefenseReadyAt!.Value-factory.Clock.GetUtcNow()).TotalSeconds,25,30);
    }

    [Fact]
    public async Task Personal_transfer_keeps_basis_and_journal_pages_do_not_expose_other_private_goods()
    {
        var v=await players.FoundVillageAsync("Owned inventory");var a=await players.JoinAsync(v,"Donor");var b=await players.JoinAsync(v,"Recipient");
        await View(a.Client,v.Path);await players.ArrangeCitizenAsync(a.Id,(p,_)=>PersonalInventory.Add(p,ResourceKind.Stone,9,99));
        var recipient=await players.LoadCitizenAsync(b.Id);
        var request=new{requestId=Guid.NewGuid(),action="transfer",resource="Stone",amount=3,target=recipient.AccountId};
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        var av=await View(a.Client,v.Path);var bv=await View(b.Client,v.Path);
        Assert.Equal(6,Assert.Single(av.Inventory).Amount);Assert.Equal(3,Assert.Single(bv.Inventory).Amount);
        Assert.Equal(99,av.Inventory.Sum(x=>x.CostBasis)+bv.Inventory.Sum(x=>x.CostBasis));
        Assert.DoesNotContain(av.Journal,x=>x.Location=="personal:"+recipient.AccountId);
        await using var db=await Db();var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var now=factory.Clock.GetUtcNow();
        for(var i=0;i<55;i++)db.LedgerEntries.Add(new LedgerEntry{VillageId=city.Id,OperationId=Guid.NewGuid(),Operation="test",Asset="Wood",Location="storage:3,3",Delta=1,At=now});
        await db.SaveChangesAsync();var first=await View(a.Client,v.Path);Assert.NotNull(first.NextJournalBefore);Assert.Equal(50,first.Journal.Count);
        var page=await a.Client.GetFromJsonAsync<EconomyView>(v.Path+"/economy?before="+first.NextJournalBefore,JsonOptions);
        Assert.NotNull(page);Assert.DoesNotContain(page.Journal,x=>first.Journal.Any(p=>p.Id==x.Id));
    }

    [Fact]
    public async Task Mentions_use_same_village_citizen_ids_and_remain_plain_text()
    {
        var v=await players.FoundVillageAsync("Mentions");var a=await players.JoinAsync(v,"Writer");var b=await players.JoinAsync(v,"Reader");
        var sent=await a.Client.PostAsJsonAsync(v.Path+"/chat",new{text="@Reader привет <b>без HTML</b>",mentions=new[]{b.Id}});
        var data=await Players.OkJsonAsync(sent);Assert.Equal(b.Id,data.GetProperty("mentionCitizenIds")[0].GetGuid());
        Assert.Contains("<b>",data.GetProperty("text").GetString());
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/chat",new{text="Не тот адресат",mentions=new[]{Guid.NewGuid()}}),"chat.mentions");
        var history=await a.Client.GetFromJsonAsync<System.Text.Json.JsonElement>(v.Path+"/chat");
        Assert.Single(history.EnumerateArray());
    }

    [Fact]
    public async Task External_blueprint_is_owned_and_cannot_draw_common_material_or_other_workers()
    {
        var v=await players.FoundVillageAsync("Private land");var a=await players.JoinAsync(v,"Owner");var b=await players.JoinAsync(v,"Other");
        var definition=BuildingCatalog.Get(BuildingKind.Hut);
        var stage=Enumerable.Range(0,definition.Stages.Count).First(i=>definition.Stages[i].Jobs.Any(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Haul));
        var job=definition.Stages[stage].Jobs.First(j=>BuildingCatalog.FindJob(j.JobId)?.Kind==JobKind.Haul);
        var resource=BuildingCatalog.FindJob(job.JobId)!.Resource!.Value;
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{
            Array.Fill(city.Explored,(byte)255);city.Wilds!.Ravine.Clear();city.Wilds.Obstacles.Clear();
            city.Structures.Add(new Structure{Kind=BuildingKind.LeanTo,PlotId="4,18",BuiltAt=now});
            var s=StorageBuilding.Create(StorageKind.TownHall,"3,3",now);s.Put(resource,10);city.Storages.Add(s);
        });
        var request=new{requestId=Guid.NewGuid(),action="private-build",building="Hut",x=-8,y=10};
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/job",new{plotId="-8,10",stage,jobId=job.JobId}),"site.noMaterial");
        await Players.AssertRuleBrokenAsync(await b.Client.PostAsJsonAsync(v.Path+"/me/job",new{plotId="-8,10",stage,jobId=job.JobId}),"building.private");
        await using var db=await Db();var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug);var owner=await players.LoadCitizenAsync(a.Id);
        Assert.Equal(owner.AccountId,Assert.Single(city.Sites).OwnerAccountId);Assert.Equal(10,city.Stock(resource));
        await Players.AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="private-build",building="Hut",x=40,y=10}),"bridge.closed");
        Assert.Single((await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug)).Sites);
    }
}
