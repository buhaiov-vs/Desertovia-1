using System.Net.Http.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;
namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class PlayerFeedback077ApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    [Fact]
    public async Task Manual_meal_uses_owned_food_once_and_does_not_restore_energy()
    {
        var v=await players.FoundVillageAsync("Meal town");var a=await players.JoinAsync(v,"Hungry");
        await players.ArrangeCitizenAsync(a.Id,(c,now)=>{c.IsHungry=true;c.StoredEnergy=3;c.EnergyUpdatedAt=now;c.Carry(ResourceKind.Food,2);});
        var result=await OkJsonAsync(await a.Client.PostAsync(v.Path+"/me/eat",null));
        Assert.False(result.GetProperty("isHungry").GetBoolean());
        await AssertRuleBrokenAsync(await a.Client.PostAsync(v.Path+"/me/eat",null),"food.notHungry");
        var saved=await players.LoadCitizenAsync(a.Id);Assert.Equal(1,saved.Carried(ResourceKind.Food));Assert.InRange(saved.StoredEnergy,3,3.01);
    }

    [Fact]
    public async Task Manual_meal_with_no_owned_food_does_not_spend_public_stock_or_bypass_medical_restrictions()
    {
        var v=await players.FoundVillageAsync("Meal guards");var a=await players.JoinAsync(v,"Hungry");
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{var store=StorageBuilding.Create(StorageKind.Heap,"1,1",now);store.Put(ResourceKind.Food,5);city.Storages.Add(store);});
        await players.ArrangeCitizenAsync(a.Id,(c,_)=>c.IsHungry=true);
        await AssertRuleBrokenAsync(await a.Client.PostAsync(v.Path+"/me/eat",null),"food.missing");
        await players.ArrangeCitizenAsync(a.Id,(c,now)=>{c.Carry(ResourceKind.Food,1);c.ExhaustedUntil=now.AddHours(1);});
        await AssertRuleBrokenAsync(await a.Client.PostAsync(v.Path+"/me/eat",null),"inventory.unavailable");
        await using var db=await Db();Assert.Equal(5,(await db.Villages.SingleAsync(x=>x.Slug==v.Slug)).Stock(ResourceKind.Food));
        Assert.Equal(1,(await players.LoadCitizenAsync(a.Id)).Carried(ResourceKind.Food));
    }

    [Fact]
    public async Task Owner_moves_into_own_home_without_approval_and_completed_owner_info_and_number_survive_reload()
    {
        var v=await players.FoundVillageAsync("Own home");var a=await players.JoinAsync(v,"Householder");var person=await players.LoadCitizenAsync(a.Id);
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>city.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="-4,10",OwnerAccountId=person.AccountId,BuiltAt=now}));
        var state=await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/civic",new{plotId="-4,10",operation="housing"}));
        Assert.DoesNotContain(state.GetProperty("civicOrders").EnumerateArray(),o=>o.GetProperty("operation").GetString()=="housing"&&o.GetProperty("status").GetString()=="pending");
        var home=Assert.Single(state.GetProperty("housing").EnumerateArray(),h=>h.GetProperty("plotId").GetString()=="-4,10");
        Assert.Equal(person.AccountId,home.GetProperty("ownerAccountId").GetGuid());Assert.Equal("Householder",home.GetProperty("ownerName").GetString());Assert.True(home.GetProperty("number").GetInt32()>0);
        Assert.Equal("-4,10",(await players.LoadCitizenAsync(a.Id)).HomePlotId);
        var reloaded=await OkJsonAsync(await a.Client.GetAsync(v.Path));
        var building=Assert.Single(reloaded.GetProperty("state").GetProperty("structures").EnumerateArray(),b=>b.GetProperty("plotId").GetString()=="-4,10");
        Assert.Equal(home.GetProperty("number").GetInt32(),building.GetProperty("number").GetInt32());
    }

    [Fact]
    public async Task Stranger_cannot_move_into_private_home_and_public_home_still_requires_approval()
    {
        var v=await players.FoundVillageAsync("Home access");var a=await players.JoinAsync(v,"Owner");var b=await players.JoinAsync(v,"Stranger");var owner=await players.LoadCitizenAsync(a.Id);
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{city.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="-4,10",OwnerAccountId=owner.AccountId,BuiltAt=now});city.Structures.Add(new(){Kind=BuildingKind.Longhouse,PlotId="1,1",BuiltAt=now});});
        await AssertRuleBrokenAsync(await b.Client.PostAsJsonAsync(v.Path+"/civic",new{plotId="-4,10",operation="housing"}),"housing.private");
        await OkJsonAsync(await b.Client.GetAsync(v.Path+"/me"));
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>city.Structures.Add(new(){Kind=BuildingKind.Longhouse,PlotId="5,1",BuiltAt=now}));
        var state=await OkJsonAsync(await b.Client.PostAsJsonAsync(v.Path+"/civic",new{plotId="5,1",operation="housing"}));
        Assert.Contains(state.GetProperty("civicOrders").EnumerateArray(),o=>o.GetProperty("operation").GetString()=="housing"&&o.GetProperty("status").GetString()=="pending");
        Assert.NotEqual("5,1",(await players.LoadCitizenAsync(b.Id)).HomePlotId);
    }

    [Fact]
    public async Task Donation_is_free_partial_and_idempotent_and_retains_remaining_cost_basis()
    {
        var v=await players.FoundVillageAsync("Donation town");var a=await players.JoinAsync(v,"Donor");
        await players.ArrangeCitizenAsync(a.Id,(c,_)=>PersonalInventory.Add(c,ResourceKind.Wood,5,35));
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{city.Storages.Clear();var s=StorageBuilding.Create(StorageKind.Heap,"1,1",now);s.Put(ResourceKind.Wood,s.FreeSpace(ResourceKind.Wood)-2);city.Storages.Add(s);});
        var request=new{requestId=Guid.NewGuid(),action="donate-stock",resource="Wood",amount=5};
        var first=await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        var second=await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",request));
        Assert.Equal(first.GetProperty("coins").GetInt64(),second.GetProperty("coins").GetInt64());
        var saved=await players.LoadCitizenAsync(a.Id);Assert.Equal(3,PersonalInventory.Amount(saved,ResourceKind.Wood));Assert.Equal(21,saved.PersonalGoods.Single().CostBasis);
        await using var db=await Db();var entries=await db.LedgerEntries.Where(e=>e.OperationId==request.requestId).ToListAsync();Assert.Equal(2,entries.Count);Assert.Equal(0,entries.Sum(e=>e.Delta));Assert.DoesNotContain(entries,e=>e.Asset=="DZK");
        await AssertRuleBrokenAsync(await a.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="donate-stock",resource="Wood",amount=2}),"storage.targetFull");
        Assert.Equal(3,PersonalInventory.Amount(await players.LoadCitizenAsync(a.Id),ResourceKind.Wood));
    }

    [Fact]
    public async Task Explicit_work_target_is_saved_and_survives_separate_requests()
    {
        var v=await players.FoundVillageAsync("Target work");var a=await players.JoinAsync(v,"Repairer");
        await players.ArrangeVillageAsync(v.Slug,(city,now)=>{var s=StorageBuilding.Create(StorageKind.Heap,"1,1",now);s.Damage=30;s.Put(ResourceKind.Wood,4);s.Put(ResourceKind.Stone,2);city.Storages.Add(s);});
        await OkJsonAsync(await a.Client.PostAsJsonAsync(v.Path+"/me/task",new{taskId="repair",workplacePlotId="1,1"}));
        Assert.Equal("1,1",(await players.LoadCitizenAsync(a.Id)).CurrentTargetPlotId);
        factory.Clock.Advance(TimeSpan.FromMinutes(30));await OkJsonAsync(await a.Client.GetAsync(v.Path+"/me"));
        await using var db=await Db();Assert.Equal(20,(await db.Villages.SingleAsync(x=>x.Slug==v.Slug)).Storages.Single(s=>s.PlotId=="1,1").Damage);
    }
}
