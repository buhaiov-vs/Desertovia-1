using System.Net;
using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Projects;
using Desertod.Domain.Technologies;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;
namespace Desertod.Api.Tests;
[Collection(ApiCollection.Name)]
public sealed class FortificationUpgradeApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelling_middle_blueprint_preserves_other_sites_nested_progress_and_hands(bool civic)
    {
        var v=await players.FoundVillageAsync("Cancel fortifications");
        var resident=await players.JoinAsync(v);
        var id=await Arrange(v);
        ConstructionSite WorkedSite(string plot)
        {
            var site=new ConstructionSite {Kind=BuildingKind.Hut,PlotId=plot,PlacedAt=factory.Clock.GetUtcNow()};
            site.Record(0,site.Jobs(0)[0].JobId,2,resident.Id);
            return site;
        }
        await players.ArrangeVillageAsync(v.Slug,(city,_)=>city.Sites.Add(WorkedSite("30,30")));
        await OkJsonAsync(await v.Tsar.PostAsync(v.Path+"/fortifications/upgrade",null));
        await players.ArrangeVillageAsync(v.Slug,(city,_)=>city.Sites.Add(WorkedSite("35,35")));
        string before;
        await using(var db=await Db())
        {
            var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==id);
            before=System.Text.Json.JsonSerializer.Serialize(city.Sites.Where(s=>s.PlotId!=FortificationUpgrades.Plot));
        }
        Task<HttpResponseMessage> Cancel(HttpClient client)=>civic
            ?client.PostAsJsonAsync(v.Path+"/civic",new{plotId=FortificationUpgrades.Plot,operation="site-cancel"})
            :client.DeleteAsync(v.Path+"/blueprints/"+FortificationUpgrades.Plot);
        Assert.Equal(HttpStatusCode.Forbidden,(await Cancel(resident.Client)).StatusCode);
        var state=await OkJsonAsync(await Cancel(v.Tsar));
        Assert.Equal(2,state.GetProperty("sites").GetArrayLength());
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Id==id);
            Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(city.Sites));
            Assert.Equal("cancelled",city.CivicOrders.Single().Status);
            Assert.Equal(1,FortificationUpgrades.Level(city));
            Assert.Equal(42,city.Fortifications[0].Damage);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK,(await v.Tsar.GetAsync(v.Path)).StatusCode);
        await OkJsonAsync(await v.Tsar.PostAsync(v.Path+"/fortifications/upgrade",null));
    }
    private async Task<Guid> Arrange(FoundedVillage v)
    {
        await using var db=await Db();var city=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
        city.Research.Add(new(){TechnologyId=TechnologyCatalog.Carpentry,ResearchedAt=factory.Clock.GetUtcNow()});
        city.Research.Add(new(){TechnologyId=TechnologyCatalog.Mining,ResearchedAt=factory.Clock.GetUtcNow()});
        db.VillageProjects.Add(new(){VillageId=city.Id,DefinitionId=ProjectCatalog.Palisade,Status=ProjectStatus.Completed,StartedAt=factory.Clock.GetUtcNow()});
        Fortifications.Ensure(city,[ProjectCatalog.Palisade]);city.Fortifications[0].Damage=42;
        await db.SaveChangesAsync();return city.Id;
    }
    [Fact] public async Task Owner_creates_costed_construction_resident_is_forbidden_and_repeat_never_starts_another_upgrade()
    {
        var v=await players.FoundVillageAsync("Fortification work");var resident=await players.JoinAsync(v);var id=await Arrange(v);
        var path=v.Path+"/fortifications/upgrade";
        Assert.Equal(HttpStatusCode.Forbidden,(await resident.Client.PostAsync(path,null)).StatusCode);
        var state=await OkJsonAsync(await v.Tsar.PostAsync(path,null));
        var site=Assert.Single(state.GetProperty("sites").EnumerateArray());
        Assert.Equal("FortificationUpgrade",site.GetProperty("kind").GetString());Assert.Equal(FortificationUpgrades.Plot,site.GetProperty("plotId").GetString());
        Assert.True(site.GetProperty("stages")[0].GetProperty("jobs")[0].GetProperty("required").GetInt32()>0);
        await AssertRuleBrokenAsync(await v.Tsar.PostAsync(path,null),"defense.upgradeWorking");
        await using var db=await Db();var city=await db.Villages.AsNoTracking().SingleAsync(x=>x.Id==id);
        Assert.Single(city.Sites);Assert.Equal(1,FortificationUpgrades.Level(city));Assert.Equal(42,city.Fortifications[0].Damage);
    }
    [Fact] public async Task Governor_request_uses_owner_approval_and_rechecks_a_wave_announced_after_the_request()
    {
        var v=await players.FoundVillageAsync("Fortification vote");var helper=await players.JoinAsync(v);var id=await Arrange(v);
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(v.Path+$"/citizens/{helper.Id}/governor",new{isGovernor=true}));
        var state=await OkJsonAsync(await helper.Client.PostAsync(v.Path+"/fortifications/upgrade",null));
        Assert.Empty(state.GetProperty("sites").EnumerateArray());var order=Assert.Single(state.GetProperty("civicOrders").EnumerateArray());var orderId=order.GetProperty("id").GetGuid();
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Id==id);city.Raids.Add(new(){Enemy=EnemyKind.Zombie,AnnouncedAt=factory.Clock.GetUtcNow(),StrikesAt=factory.Clock.GetUtcNow().AddHours(1)});
            db.ExternalLogins.Add(new(){AccountId=city.OwnerAccountId,Provider="telegram",ProviderUserId="71073104"});await db.SaveChangesAsync();
        }
        var notice=await factory.Services.GetRequiredService<GameService>().ClaimCivicNoticeAsync(id,default);
        Assert.Equal("FortificationUpgrade",notice!.BuildingKind);Assert.Equal(0,notice.DisplayNumber);
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+$"/civic/{orderId}",new{approve=true}),"defense.raidPending");
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Id==id);Assert.Empty(city.Sites);Assert.Equal("pending",city.CivicOrders.Single().Status);
            city.Raids.Single().Outcome=RaidOutcome.Cancelled;await db.SaveChangesAsync();
        }
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+$"/civic/{orderId}",new{approve=true}));
        await using(var db=await Db())Assert.Single((await db.Villages.SingleAsync(x=>x.Id==id)).Sites);
    }
    [Fact] public async Task Completed_paid_work_survives_reload_waits_for_raid_then_applies_once_without_repair()
    {
        var v=await players.FoundVillageAsync("Fortification wait");var id=await Arrange(v);
        await OkJsonAsync(await v.Tsar.PostAsync(v.Path+"/fortifications/upgrade",null));
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Id==id);city.Sites.Single().Stage=1;
            city.Raids.Add(new(){Enemy=EnemyKind.Zombie,AnnouncedAt=factory.Clock.GetUtcNow(),StrikesAt=factory.Clock.GetUtcNow().AddHours(1)});await db.SaveChangesAsync();
        }
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Id==id);Assert.Single(city.Sites);Assert.Equal(1,FortificationUpgrades.Level(city));
            city.Raids.Single().Outcome=RaidOutcome.Cancelled;await db.SaveChangesAsync();
        }
        await using(var db=await Db())
        {
            var city=await db.Villages.SingleAsync(x=>x.Id==id);Assert.Empty(city.Sites);Assert.Equal(2,FortificationUpgrades.Level(city));
            Assert.Equal(42,city.Fortifications[0].Damage);await db.SaveChangesAsync();
            Assert.Equal(2,FortificationUpgrades.Level(city));
        }
    }
}
