using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Desertod.Api.Game;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;
namespace Desertod.Api.Tests;
[Collection(ApiCollection.Name)]
public sealed class FreeCombatApiTests(DesertodApiFactory factory)
{
    private readonly Players players=new(factory);
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    [Fact] public async Task Ordinary_idle_resident_can_choose_an_enemy_without_guard_duty_and_reentry_keeps_health()
    {
        var v=await players.FoundVillageAsync("Free combat");var worker=await players.JoinAsync(v);var battleId=Guid.CreateVersion7();var target=Guid.CreateVersion7();
        await using(var db=await Db())
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==worker.Id);c.CurrentTaskId=null;c.CycleSettled=true;
            var now=factory.Clock.GetUtcNow();db.RaidBattles.Add(new(){Id=battleId,VillageId=c.VillageId,Sector="west",Phase="Fighting",StartsAt=now,LastTickAt=now,EndsAt=now.AddMinutes(4),
                Units=[new(){Id=target,X=1.5,Y=13.5,Health=200,MaximumHealth=200,Speed=0}]});await db.SaveChangesAsync();
        }
        var path=v.Path+$"/battles/{battleId}/order";
        Assert.Equal(HttpStatusCode.OK,(await worker.Client.PutAsJsonAsync(path,new{targetUnitId=target,mode="intercept"})).StatusCode);
        await using(var db=await Db()){var b=await db.RaidBattles.SingleAsync(b=>b.Id==battleId);var f=Assert.Single(b.Fighters);Assert.False(f.Guard);f.Health=7;f.Active=false;f.Mode="withdrawn";await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.OK,(await worker.Client.PutAsJsonAsync(path,new{targetUnitId=target,mode="intercept"})).StatusCode);
        await using(var db=await Db()){var f=(await db.RaidBattles.AsNoTracking().SingleAsync(b=>b.Id==battleId)).Fighters.Single();Assert.Equal(7,f.Health);Assert.True(f.Active);}
        var c2=await players.LoadCitizenAsync(worker.Id);Assert.Null(c2.CurrentTaskId);Assert.True(c2.CycleSettled);
    }
    [Fact] public async Task Retired_tasks_are_absent_and_rejected_and_diagnostics_are_owner_only()
    {
        var v=await players.FoundVillageAsync("Combat metrics");var worker=await players.JoinAsync(v);
        var catalog=await worker.Client.GetFromJsonAsync<JsonElement>("/api/catalog");
        Assert.DoesNotContain(catalog.GetProperty("tasks").EnumerateArray(),t=>t.GetProperty("effect").GetString()=="Guard");
        await AssertRuleBrokenAsync(await worker.Client.PostAsJsonAsync(worker.Path+"/me/task",new{taskId=GameCatalog.KeepWatch}),"task.retired");
        Assert.Equal(HttpStatusCode.Forbidden,(await worker.Client.GetAsync(v.Path+"/combat-diagnostics")).StatusCode);
        var report=await OkJsonAsync(await v.Tsar.GetAsync(v.Path+"/combat-diagnostics"));Assert.True(report.TryGetProperty("battles",out _));Assert.True(report.TryGetProperty("workingSetBytes",out _));
    }
    [Fact] public async Task Paid_legacy_duty_settles_once_without_restarting_or_resetting_energy()
    {
        var v=await players.FoundVillageAsync("Retired duty");var worker=await players.JoinAsync(v);
        await players.ArrangeCitizenAsync(worker.Id,(c,now)=>{c.CurrentTaskId=GameCatalog.KeepWatch;c.CycleStartedAt=now;c.CycleEndsAt=now.AddHours(1);c.CycleSettled=false;c.DefenseSector="north";});
        var before=await players.LoadCitizenAsync(worker.Id);var game=factory.Services.GetRequiredService<GameService>();
        await game.RetireGuardDutiesAsync(default);var after=await players.LoadCitizenAsync(worker.Id);
        Assert.True(after.CycleSettled);Assert.Null(after.CurrentTaskId);Assert.Null(after.DefenseSector);Assert.True(after.StoredEnergy>=before.StoredEnergy);
        var renown=after.Renown;await game.RetireGuardDutiesAsync(default);Assert.Equal(renown,(await players.LoadCitizenAsync(worker.Id)).Renown);
    }
    [Fact] public void Server_history_is_bounded_and_scoped_to_the_village()
    {
        var village=Guid.NewGuid();var battle=Guid.NewGuid();var now=factory.Clock.GetUtcNow();
        var logger=Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        for(var n=0;n<150;n++)CombatDiagnostics.Record(logger,village,battle,now.AddSeconds(n),10,3,new(1,1000,2,5,50,0));
        Assert.Equal(120,Assert.Single(CombatDiagnostics.Read(village)).Samples.Count);
        Assert.Empty(CombatDiagnostics.Read(Guid.NewGuid()));
    }

}
