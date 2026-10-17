using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Domain.Combat;
using Desertod.Domain.Projects;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class RaidEditorApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();

    private static object Settings(int waves=1)=>new {preset="boss",mode="manual",enemy="Skeleton",sector="west",
        count=2,waves,healthMultiplier=3,warningSeconds=60,durationSeconds=360,
        species=new[]{new{enemy="Skeleton",percent=50},new{enemy="Wolf",percent=50}}};

    [Fact]
    public async Task Preview_is_read_only_and_every_editor_route_is_owner_only()
    {
        var v=await players.FoundVillageAsync("Raid preview");var helper=await players.JoinAsync(v);
        foreach(var path in new[]{"/raid-editor/preview","/raid-editor/launch"})
        {
            var body=path.EndsWith("launch")?(object)new{requestId=Guid.NewGuid(),settings=Settings()}:Settings();
            Assert.Equal(HttpStatusCode.Forbidden,(await helper.Client.PostAsJsonAsync(v.Path+path,body)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden,(await helper.Client.GetAsync(v.Path+"/raid-editor")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await helper.Client.PutAsJsonAsync(v.Path+"/raid-editor/presets",new{name="No",settings=Settings()})).StatusCode);
        await using(var setup=await Db())
        {
            var id=(await setup.Villages.SingleAsync(x=>x.Slug==v.Slug)).Id;
            setup.VillageProjects.Add(new(){VillageId=id,DefinitionId=ProjectCatalog.Palisade,Status=ProjectStatus.Completed,StartedAt=factory.Clock.GetUtcNow()});
            await setup.SaveChangesAsync();
        }
        var preview=await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/raid-editor/preview",Settings()));
        Assert.Equal(2,preview.GetProperty("count").GetInt32());Assert.Equal("dangerous",preview.GetProperty("risk").GetString());
        await using var db=await Db();var village=await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug);
        Assert.Empty(village.Raids);Assert.Empty(village.Fortifications);
        Assert.False(await db.RaidBattles.AnyAsync(b=>b.VillageId==village.Id));
        Assert.False(await db.RaidLaunchReceipts.AnyAsync(b=>b.VillageId==village.Id));
    }

    [Fact]
    public async Task Launch_is_once_settings_survive_reload_and_cancel_stops_all_waves()
    {
        var v=await players.FoundVillageAsync("Raid waves");var requestId=Guid.NewGuid();
        var request=new{requestId,settings=Settings(3)};
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/raid-editor/launch",request));
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/raid-editor/launch",request));
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/raid-editor/launch",new{requestId,settings=Settings(2)}),"raid.requestConflict");
        Guid villageId;
        await using(var db=await Db())
        {
            villageId=(await db.Villages.AsNoTracking().SingleAsync(x=>x.Slug==v.Slug)).Id;
            var battles=await db.RaidBattles.AsNoTracking().Where(b=>b.VillageId==villageId).ToListAsync();
            Assert.Equal(3,battles.Count);Assert.Equal(1,await db.RaidLaunchReceipts.CountAsync(b=>b.VillageId==villageId));
            Assert.All(battles,b=>
            {
                Assert.Equal(requestId,b.EventId);Assert.NotNull(b.InitiatedBy);Assert.True(b.ManualOverride);
                Assert.Equal(360,(b.EndsAt-b.StartsAt).TotalSeconds);Assert.Equal(3,b.Settings!.HealthMultiplier);
                Assert.Equal(2,b.Units.Count);Assert.All(b.Units,u=>Assert.Equal("boss",u.Variant));
                Assert.Contains(b.Units,u=>u.Enemy==EnemyKind.Wolf);Assert.Contains(b.Units,u=>u.Enemy==EnemyKind.Skeleton);
            });
        }
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/raid-editor/launch",new{requestId=Guid.NewGuid(),settings=Settings()}),"raid.pending");
        await OkJsonAsync(await v.Tsar.DeleteAsync(v.Path+"/undead"));
        await using(var db=await Db())
        {
            var battles=await db.RaidBattles.AsNoTracking().Where(b=>b.VillageId==villageId).ToListAsync();
            Assert.All(battles,b=>{Assert.NotNull(b.FinishedAt);Assert.All(b.Units,u=>Assert.False(u.Alive));});
            Assert.Empty((await db.Villages.SingleAsync(x=>x.Id==villageId)).RaidAdaptation.Recent);
            await db.RaidBattles.Where(b=>b.VillageId==villageId).ExecuteDeleteAsync();
        }
        // The compact receipt remains after detailed battle history is pruned.
        await OkJsonAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/raid-editor/launch",request));
        await using(var db=await Db())Assert.False(await db.RaidBattles.AnyAsync(b=>b.VillageId==villageId));
    }

    [Fact]
    public async Task Custom_preset_roundtrips_and_an_unaffordable_auto_boss_is_explained()
    {
        var v=await players.FoundVillageAsync("Saved raids");
        var saved=await OkJsonAsync(await v.Tsar.PutAsJsonAsync(v.Path+"/raid-editor/presets",new{name="Два командира",settings=Settings()}));
        var id=saved.GetProperty("presets")[0].GetProperty("id").GetGuid();
        await OkJsonAsync(await v.Tsar.PutAsJsonAsync(v.Path+"/raid-editor/presets",new{id,name="Смешанные боссы",settings=Settings(2)}));
        var loaded=await v.Tsar.GetFromJsonAsync<JsonElement>(v.Path+"/raid-editor");
        Assert.Single(loaded.GetProperty("presets").EnumerateArray());
        Assert.Equal("Смешанные боссы",loaded.GetProperty("presets")[0].GetProperty("name").GetString());
        Assert.Equal(2,loaded.GetProperty("presets")[0].GetProperty("settings").GetProperty("waves").GetInt32());
        Assert.Equal(1,loaded.GetProperty("coefficient").GetDouble());
        await AssertRuleBrokenAsync(await v.Tsar.PostAsJsonAsync(v.Path+"/raid-editor/preview",new{preset="boss",mode="automatic",enemy="Skeleton",count=2}),"raid.unaffordable");
    }
}
