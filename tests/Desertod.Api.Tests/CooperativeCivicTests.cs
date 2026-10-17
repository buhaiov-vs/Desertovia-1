using System.Net.Http.Json;
using Desertod.Domain.Buildings;
using Desertod.Domain.Storage;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;
namespace Desertod.Api.Tests;
[Collection(ApiCollection.Name)]
public sealed class CooperativeCivicTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    [Fact] public async Task Governor_prepares_demolition_but_only_owner_starts_it_and_ids_survive_reload()
    {
        var village=await players.FoundVillageAsync("Civic Realm");
        var governor=await players.JoinAsync(village,"Governor");
        await using(var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync())
        {
            var ruler=await db.Citizens.SingleAsync(c=>c.Id==governor.Id);ruler.IsGovernor=true;
            var city=await db.Villages.SingleAsync(v=>v.Id==ruler.VillageId);
            city.Storages.Add(StorageBuilding.Create(StorageKind.WoodPile,"1,1",factory.Clock.GetUtcNow()));await db.SaveChangesAsync();
        }
        var requested=await OkJsonAsync(await governor.Client.PostAsJsonAsync(village.Path+"/civic",new{plotId="1,1",operation="demolish"}));
        var order=requested.GetProperty("civicOrders")[0];var id=order.GetProperty("id").GetString();
        Assert.Empty(requested.GetProperty("sites").EnumerateArray());
        await AssertForbiddenAsync(await governor.Client.PostAsJsonAsync(village.Path+$"/civic/{id}",new{approve=true}));
        var state=await OkJsonAsync(await village.Tsar.PostAsJsonAsync(village.Path+$"/civic/{id}",new{approve=true}));
        Assert.Single(state.GetProperty("sites").EnumerateArray());
        var reloaded=await OkJsonAsync(await village.Tsar.GetAsync(village.Path));
        Assert.Equal(id,reloaded.GetProperty("state").GetProperty("civicOrders")[0].GetProperty("id").GetString());
        await AssertRuleBrokenAsync(await village.Tsar.PostAsJsonAsync(village.Path+$"/civic/{id}",new{approve=true}),"building.orderProcessed");
    }
    [Theory]
    [InlineData("Hospital")]
    [InlineData("Castle")]
    [InlineData("WoodPile")]
    public async Task Governor_requests_any_building_without_starting_work_and_only_Tsar_approves(string kind)
    {
        var village = await players.FoundVillageAsync("Construction approval");
        if(kind=="Castle")
        {
            await using var legacy=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
            var city=await legacy.Villages.SingleAsync(x=>x.Slug==village.Slug);city.Structures.RemoveAll(s=>s.Kind==BuildingKind.Castle);await legacy.SaveChangesAsync();
        }
        var governor = await players.JoinAsync(village, "Planner");
        var citizen = await players.JoinAsync(village, "Worker");
        await OkJsonAsync(await village.Tsar.PutAsJsonAsync(village.Path + $"/citizens/{governor.Id}/governor", new { isGovernor = true }));
        var blueprint = new { kind, x = 9, y = 11 };
        await AssertForbiddenAsync(await citizen.Client.PostAsJsonAsync(village.Path + "/blueprints", blueprint));
        var requested = await OkJsonAsync(await governor.Client.PostAsJsonAsync(village.Path + "/blueprints", blueprint));
        Assert.Empty(requested.GetProperty("sites").EnumerateArray());
        var order = requested.GetProperty("civicOrders")[0];
        Assert.Equal("build", order.GetProperty("operation").GetString());
        Assert.Equal(kind, order.GetProperty("buildingKind").GetString());
        Assert.Equal("pending", order.GetProperty("status").GetString());
        var id = order.GetProperty("id").GetString();
        await AssertRuleBrokenAsync(await governor.Client.PostAsJsonAsync(village.Path + "/blueprints", blueprint), "building.orderExists");
        await AssertForbiddenAsync(await governor.Client.PostAsJsonAsync(village.Path + $"/civic/{id}", new { approve = true }));
        var reloaded = await OkJsonAsync(await village.Tsar.GetAsync(village.Path));
        Assert.Equal(kind, reloaded.GetProperty("state").GetProperty("civicOrders")[0].GetProperty("buildingKind").GetString());
        var approved = await OkJsonAsync(await village.Tsar.PostAsJsonAsync(village.Path + $"/civic/{id}", new { approve = true }));
        var site = Assert.Single(approved.GetProperty("sites").EnumerateArray());
        Assert.Equal(kind, site.GetProperty("kind").GetString());
        Assert.Equal("9,11", site.GetProperty("plotId").GetString());
        await AssertRuleBrokenAsync(await village.Tsar.PostAsJsonAsync(village.Path + $"/civic/{id}", new { approve = true }), "building.orderProcessed");
    }

    [Fact]
    public async Task Construction_approval_rechecks_occupied_plot_and_rejection_starts_nothing()
    {
        var village = await players.FoundVillageAsync("Changed plot");
        var governor = await players.JoinAsync(village, "Planner");
        await OkJsonAsync(await village.Tsar.PutAsJsonAsync(village.Path + $"/citizens/{governor.Id}/governor", new { isGovernor = true }));
        var blueprint = new { kind = "Hospital", x = 9, y = 11 };
        var requested = await OkJsonAsync(await governor.Client.PostAsJsonAsync(village.Path + "/blueprints", blueprint));
        var id = requested.GetProperty("civicOrders")[0].GetProperty("id").GetString();
        await OkJsonAsync(await village.Tsar.PostAsJsonAsync(village.Path + "/blueprints", new { kind = "WoodPile", x = 9, y = 11 }));
        await AssertRuleBrokenAsync(await village.Tsar.PostAsJsonAsync(village.Path + $"/civic/{id}", new { approve = true }), "building.plotTaken");
        var rejected = await OkJsonAsync(await village.Tsar.PostAsJsonAsync(village.Path + $"/civic/{id}", new { approve = false }));
        Assert.Equal("rejected", rejected.GetProperty("civicOrders")[0].GetProperty("status").GetString());
        Assert.Equal("WoodPile", Assert.Single(rejected.GetProperty("sites").EnumerateArray()).GetProperty("kind").GetString());
    }

    [Fact] public async Task Resource_and_walk_API_cannot_bypass_closed_bridge()
    {
        var village=await players.FoundVillageAsync("Bank Guard");var player=await players.JoinAsync(village);
        await AssertRuleBrokenAsync(await player.Client.PostAsJsonAsync(village.Path+"/me/goto",new{spot=new{x=40,y=17}}),"bridge.closed");
        await AssertRuleBrokenAsync(await player.Client.PostAsJsonAsync(village.Path+"/me/task",new{taskId="chop-wood",spot=new{x=40,y=17}}),"bridge.closed");
    }

    [Fact] public async Task Wounded_is_delivered_once_and_recovers_faster_without_losing_items()
    {
        var village=await players.FoundVillageAsync("Rescue Town");var helper=await players.JoinAsync(village,"Rescuer");var patient=await players.JoinAsync(village,"Wounded");
        var until=DateTimeOffset.FromUnixTimeMilliseconds(factory.Clock.GetUtcNow().AddHours(1).ToUnixTimeMilliseconds());
        await using(var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync())
        {
            var p=await db.Citizens.SingleAsync(c=>c.Id==patient.Id);p.InjuredUntil=until;p.WoundedAt=factory.Clock.GetUtcNow();
            p.RescueRewardEligible=true;
            var v=await db.Villages.SingleAsync(v=>v.Id==p.VillageId);
            v.Structures.Add(new(){Kind=BuildingKind.Hospital,PlotId="20,15",BuiltAt=factory.Clock.GetUtcNow()});
            p.IdleSpot=new(12,16);p.Carry(Desertod.Domain.Catalog.ResourceKind.Wood,3);await db.SaveChangesAsync();
        }
        var started=await OkJsonAsync(await helper.Client.PostAsync(village.Path+$"/rescue/{patient.Id}",null));
        Assert.Equal("rescue-wounded",started.GetProperty("taskId").GetString());
        await AssertRuleBrokenAsync(await helper.Client.PostAsync(village.Path+$"/rescue/{patient.Id}",null),"task.cycleRunning");
        var arrival=started.GetProperty("cycleEndsAt").GetDateTimeOffset();
        factory.Clock.Advance(arrival-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));
        await OkJsonAsync(await helper.Client.GetAsync(village.Path+"/me"));
        await factory.Services.GetRequiredService<Desertod.Api.Game.GameService>().RefreshHospitalAsync(default);
        await using var check=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var saved=await check.Citizens.SingleAsync(c=>c.Id==patient.Id);
        Assert.NotNull(saved.HospitalizedAt);Assert.True(saved.InjuredUntil<until);
        var expected=saved.HospitalizedAt!.Value+TimeSpan.FromTicks((long)((until-saved.HospitalizedAt.Value).Ticks*.25));
        Assert.True(Math.Abs((saved.InjuredUntil!.Value-expected).TotalMilliseconds)<1);
        Assert.Equal(3,saved.Carried(Desertod.Domain.Catalog.ResourceKind.Wood));
        Assert.True((await check.Citizens.SingleAsync(c=>c.Id==helper.Id)).Renown>=2);
        await AssertRuleBrokenAsync(await helper.Client.PostAsync(village.Path+$"/rescue/{patient.Id}",null),"rescue.notNeeded");
    }
    [Fact] public async Task Nobody_automatically_admits_an_unclaimed_patient_after_five_minutes()
    {
        var village=await players.FoundVillageAsync("Orderlies");var patient=await players.JoinAsync(village);
        await using(var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync())
        {
            var p=await db.Citizens.SingleAsync(c=>c.Id==patient.Id);p.InjuredUntil=factory.Clock.GetUtcNow().AddHours(1);p.WoundedAt=factory.Clock.GetUtcNow();await db.SaveChangesAsync();
        }
        factory.Clock.Advance(TimeSpan.FromMinutes(6));
        await factory.Services.GetRequiredService<Desertod.Api.Game.GameService>().RefreshHospitalAsync(default);
        await using var check=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var saved=await check.Citizens.SingleAsync(c=>c.Id==patient.Id);Assert.Null(saved.HospitalizedAt);Assert.True(saved.InjuredUntil>factory.Clock.GetUtcNow());
    }

    [Fact] public async Task Wounded_requests_help_once_per_minute_without_being_admitted()
    {
        var village=await players.FoundVillageAsync("Help request");var patient=await players.JoinAsync(village);
        await using(var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync())
        {
            var p=await db.Citizens.SingleAsync(c=>c.Id==patient.Id);
            p.InjuredUntil=factory.Clock.GetUtcNow().AddHours(1);p.WoundedAt=factory.Clock.GetUtcNow();await db.SaveChangesAsync();
        }
        await OkJsonAsync(await patient.Client.PostAsync(village.Path+"/rescue/request",null));
        await OkJsonAsync(await patient.Client.PostAsync(village.Path+"/rescue/request",null));
        await using var check=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var saved=await check.Citizens.SingleAsync(c=>c.Id==patient.Id);
        Assert.NotNull(saved.RescueRequestedAt);Assert.Null(saved.HospitalizedAt);
        Assert.Equal(1,await check.ChatMessages.CountAsync(m=>m.VillageId==saved.VillageId&&m.CitizenId==patient.Id));
    }

    [Fact] public async Task Tsar_can_request_help_and_be_carried_without_a_second_character()
    {
        var village=await players.FoundVillageAsync("Royal rescue");var helper=await players.JoinAsync(village);
        Guid actor=default;
        await players.ArrangeVillageAsync(village.Slug,(v,now)=>
        {
            actor=v.OwnerAccountId;v.TsarWoundedAt=now;v.TsarInjuredUntil=now.AddMinutes(40);
            v.TsarRescueRewardEligible=true;v.TsarSpot=new(16.5,16.5);
            v.Structures.Add(new(){Kind=BuildingKind.Hospital,PlotId="20,15",BuiltAt=now});
        });
        await OkJsonAsync(await village.Tsar.PostAsync(village.Path+"/rescue/request",null));
        var started=await OkJsonAsync(await helper.Client.PostAsync(village.Path+$"/rescue/{actor}",null));
        var arrival=started.GetProperty("cycleEndsAt").GetDateTimeOffset();
        factory.Clock.Advance(arrival-factory.Clock.GetUtcNow()+TimeSpan.FromSeconds(1));
        await OkJsonAsync(await helper.Client.GetAsync(village.Path+"/me"));
        await using var check=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var saved=await check.Villages.SingleAsync(v=>v.Slug==village.Slug);
        Assert.NotNull(saved.TsarHospitalizedAt);Assert.Null(saved.TsarRescueByCitizenId);
        Assert.True(saved.TsarInjuredUntil<saved.TsarWoundedAt!.Value.AddMinutes(20));
        Assert.False(await check.Citizens.AnyAsync(c=>c.AccountId==actor));
        Assert.True((await check.Citizens.SingleAsync(c=>c.Id==helper.Id)).Renown>=2);
    }
}
