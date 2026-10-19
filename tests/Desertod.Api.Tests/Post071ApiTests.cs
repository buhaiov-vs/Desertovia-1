using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;
namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class Post071ApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    [Fact]
    public async Task Owner_without_citizen_can_target_as_voivode_but_commands_do_not_deal_damage()
    {
        var v=await players.FoundVillageAsync("Tsar defense");
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/undead",new{enemy="Skeleton",size="Small"}));
        Guid battleId,target;double hp;
        await using(var db=await Db())
        {var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var b=await db.RaidBattles.SingleAsync(x=>x.VillageId==village.Id);battleId=b.Id;target=b.Units[0].Id;hp=b.Units[0].Health;}
        for(var n=0;n<3;n++)Assert.Equal(HttpStatusCode.OK,(await v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{battleId}/order",new{targetUnitId=target,mode="intercept"})).StatusCode);
        await using(var db=await Db())
        {
            var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var b=await db.RaidBattles.AsNoTracking().SingleAsync(x=>x.Id==battleId);var f=Assert.Single(b.Fighters);
            Assert.True(f.IsTsar);Assert.Equal(village.OwnerAccountId,f.AccountId);Assert.Equal(Guid.Empty,f.CitizenId);Assert.Equal(target,f.TargetUnitId);Assert.Equal(hp,b.Units[0].Health);
            Assert.False(await db.Citizens.AnyAsync(x=>x.AccountId==village.OwnerAccountId));
        }
        var stranger=await players.JoinAsync(v);
        Assert.Equal(HttpStatusCode.OK,(await stranger.Client.PutAsJsonAsync(v.Path+$"/battles/{battleId}/order",new{targetUnitId=target})).StatusCode);
        await using(var db=await Db())
        {var b=await db.RaidBattles.AsNoTracking().SingleAsync(x=>x.Id==battleId);Assert.Equal(2,b.Fighters.Count);Assert.Single(b.Fighters,f=>f.IsTsar);Assert.Equal(hp,b.Units[0].Health);}
    }
    [Fact]
    public async Task Future_wave_cannot_mint_a_second_royal_actor()
    {
        var v=await players.FoundVillageAsync("Queued commands");
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/undead",new{enemy="Zombie",size="Medium"}));
        Guid queued;
        await using(var db=await Db())
        {
            var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
            queued=(await db.RaidBattles.Where(x=>x.VillageId==village.Id).OrderBy(x=>x.ScheduledAt).ToListAsync()).Last().Id;
        }
        await AssertRuleBrokenAsync(await v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{queued}/order",new{mode="intercept"}),"raid.queued");
        await using(var db=await Db())Assert.Empty((await db.RaidBattles.SingleAsync(x=>x.Id==queued)).Fighters);
    }
    [Fact]
    public async Task Royal_actor_without_citizen_obeys_external_hospital_restriction()
    {
        var v=await players.FoundVillageAsync("Royal recovery");
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/undead",new{enemy="Skeleton",size="Small"}));
        const long uid=71907101;Guid battle;
        await using(var db=await Db())
        {
            var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);
            db.ExternalLogins.Add(new(){Provider="telegram",ProviderUserId=uid.ToString(),AccountId=village.OwnerAccountId});
            battle=(await db.RaidBattles.SingleAsync(x=>x.VillageId==village.Id)).Id;
            await db.SaveChangesAsync();
        }
        factory.Hospital.Enabled=true;
        var now=factory.Clock.GetUtcNow().ToUnixTimeSeconds();
        factory.Hospital.States[uid]=new(uid.ToString(),"royal-wound",now,now+3600,now,0,0,[]);
        try
        {
            await AssertRuleBrokenAsync(await v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{battle}/order",new{mode="intercept"}),"hospital.unavailable");
            factory.Hospital.States.Remove(uid);
            Assert.Equal(HttpStatusCode.OK,(await v.Tsar.PutAsJsonAsync(v.Path+$"/battles/{battle}/order",new{mode="intercept"})).StatusCode);
        }
        finally {factory.Hospital.Reset();}
    }
    [Fact]
    public async Task Sleep_stops_active_and_queued_undead_and_is_idempotent_without_removing_history()
    {
        var v=await players.FoundVillageAsync("Stop battle");var worker=await players.JoinAsync(v);
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/undead",new{enemy="Zombie",size="Medium"}));
        await using(var db=await Db())
        {
            var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var raid=village.Raids[0];raid.StrikesAt=factory.Clock.GetUtcNow().AddSeconds(-2);
            var b=await db.RaidBattles.SingleAsync(x=>x.Id==raid.Id);b.Phase="Fighting";b.StartsAt=raid.StrikesAt;b.LastTickAt=raid.StrikesAt;
            village.Raids.Add(new(){Enemy=EnemyKind.Wolf,Source=RaidSource.Night,StrikesAt=factory.Clock.GetUtcNow().AddHours(1),AnnouncedAt=factory.Clock.GetUtcNow()});await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden,(await worker.Client.DeleteAsync(v.Path+"/undead")).StatusCode);
        for(var n=0;n<2;n++)await OkJsonAsync(await v.Tsar.DeleteAsync(v.Path+"/undead"));
        await using(var db=await Db())
        {
            var village=await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug);
            Assert.Equal(2,village.Raids.Count(r=>r.Source==RaidSource.Undead&&r.Outcome==RaidOutcome.Cancelled));
            Assert.True(village.Raids.Single(r=>r.Source==RaidSource.Night).IsPending);
            Assert.All(await db.RaidBattles.AsNoTracking().Where(b=>b.VillageId==village.Id).ToArrayAsync(),b=>{Assert.Equal("Cancelled",b.Phase);Assert.NotNull(b.FinishedAt);Assert.All(b.Units,u=>Assert.False(u.Alive));});
        }
    }
    [Fact]
    public async Task Private_salvage_is_collectible_only_by_its_owner_and_never_twice()
    {
        var v=await players.FoundVillageAsync("Saved salvage");var worker=await players.JoinAsync(v);Guid recoveryId;
        await using(var db=await Db())
        {
            var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var c=await db.Citizens.SingleAsync(x=>x.Id==worker.Id);
            var recovery=new SiteRecovery{OwnerAccountId=c.AccountId,PlotId="26,5",CreatedAt=factory.Clock.GetUtcNow(),Goods=[new(){Kind=ResourceKind.Wood,Amount=7}]};recoveryId=recovery.Id;village.SiteRecoveries.Add(recovery);await db.SaveChangesAsync();
        }
        var hidden=await v.Tsar.GetFromJsonAsync<JsonElement>(v.Path+"/economy");Assert.Empty(hidden.GetProperty("siteRecoveries").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden,(await v.Tsar.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="claim-site-recovery",target=recoveryId})).StatusCode);
        for(var n=0;n<2;n++)await OkJsonAsync(await worker.Client.PostAsJsonAsync(v.Path+"/economy",new{requestId=Guid.NewGuid(),action="claim-site-recovery",target=recoveryId}));
        var c2=await players.LoadCitizenAsync(worker.Id);Assert.Equal(7,c2.PersonalGoods.Single(g=>g.Kind==ResourceKind.Wood).Amount);
        await using(var db=await Db())Assert.Empty((await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug)).SiteRecoveries);
    }
    [Fact]
    public async Task Tsar_can_resolve_conflicting_site_but_cannot_delete_someone_elses_legal_blueprint()
    {
        var v=await players.FoundVillageAsync("Site ownership");var worker=await players.JoinAsync(v);
        await using(var db=await Db())
        {
            var village=await db.Villages.SingleAsync(x=>x.Slug==v.Slug);var c=await db.Citizens.SingleAsync(x=>x.Id==worker.Id);
            village.Sites.Add(new(){Kind=BuildingKind.Hut,PlotId="26,5",PlacedAt=factory.Clock.GetUtcNow(),OwnerAccountId=c.AccountId,Progress=[new(){JobId="mark-outline",Stage=0,Done=1}]});
            village.Sites.Add(new(){Kind=BuildingKind.Hut,PlotId="-12,16",PlacedAt=factory.Clock.GetUtcNow(),OwnerAccountId=c.AccountId});await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden,(await v.Tsar.DeleteAsync(v.Path+"/blueprints/-12,16")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await v.Tsar.PostAsJsonAsync(v.Path+"/civic",new{plotId="-12,16",operation="site-demolish"})).StatusCode);
        var changed=await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/civic",new{plotId="26,5",operation="site-demolish"}));
        Assert.Equal("site-demolish",changed.GetProperty("sites").EnumerateArray().Single(s=>s.GetProperty("plotId").GetString()=="26,5").GetProperty("operation").GetString());
    }
    [Fact]
    public async Task Ruined_untouched_blueprint_cancel_preserves_other_sites_and_owner_rights()
    {
        var v=await players.FoundVillageAsync("Ruined blueprint");var worker=await players.JoinAsync(v);
        await players.ArrangeVillageAsync(v.Slug,(village,now)=>{
            village.Sites.Add(new(){Kind=BuildingKind.Hut,PlotId="1,1",PlacedAt=now,RuinedAt=now,Damage=100});
            village.Sites.Add(new(){Kind=BuildingKind.Hut,PlotId="5,1",PlacedAt=now});
        });
        Assert.Equal(HttpStatusCode.Forbidden,(await worker.Client.PostAsJsonAsync(v.Path+"/civic",new{plotId="1,1",operation="site-cancel"})).StatusCode);
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/civic",new{plotId="1,1",operation="site-cancel"}));
        await using var db=await Db();var village=await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug);
        Assert.DoesNotContain(village.Sites,s=>s.PlotId=="1,1");Assert.Contains(village.Sites,s=>s.PlotId=="5,1");
    }
}
