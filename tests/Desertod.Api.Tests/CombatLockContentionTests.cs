using System.Net;
using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;
[Collection(ApiCollection.Name)]
public sealed class CombatLockContentionTests(DesertodApiFactory factory) : IDisposable
{
    private readonly Players players=new(factory);
    private static long nextUid=700000;
    private Task<DesertodDbContext> Db()=>factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
    public void Dispose()=>factory.Hospital.Reset();
    // Общий HospitalWorker продолжает опрашивать другие деревни. Проверяем весь
    // набор жителей этой деревни: лишний житель в принудительном запросе всё ещё ошибка.
    private long[][] RequestsFor(long[] residents,long ruler)=>factory.Hospital.StatusRequests
        .Where(ids=>ids.Any(id=>residents.Contains(id)||id==ruler)).ToArray();

    private async Task<(FoundedVillage Village,Resident[] Residents,Guid VillageId,Guid BattleId,long[] Uids,long TsarUid)> Setup()
    {
        factory.Hospital.Reset();
        var village=await players.FoundVillageAsync("Combat queue");
        var residents=new[]{await players.JoinAsync(village),await players.JoinAsync(village),await players.JoinAsync(village)};
        var uids=residents.Select(_=>Interlocked.Increment(ref nextUid)).ToArray();var tsarUid=Interlocked.Increment(ref nextUid);
        await using var db=await Db();var v=await db.Villages.SingleAsync(v=>v.Slug==village.Slug);
        for(var i=0;i<residents.Length;i++)
        {
            var c=await db.Citizens.SingleAsync(c=>c.Id==residents[i].Id);c.CycleSettled=true;c.CurrentTaskId=null;
            db.ExternalLogins.Add(new(){Provider="telegram",ProviderUserId=uids[i].ToString(),AccountId=c.AccountId});
        }
        db.ExternalLogins.Add(new(){Provider="telegram",ProviderUserId=tsarUid.ToString(),AccountId=v.OwnerAccountId});
        var now=factory.Clock.GetUtcNow();var battle=new RaidBattle{Id=Guid.CreateVersion7(),VillageId=v.Id,Sector="west",Phase="Fighting",StartsAt=now,LastTickAt=now,EndsAt=now.AddMinutes(4),
            Units=[new(){Id=Guid.CreateVersion7(),X=1.5,Y=13.5,Health=200,MaximumHealth=200,Speed=0}]};
        db.RaidBattles.Add(battle);await db.SaveChangesAsync();
        return(village,residents,v.Id,battle.Id,uids,tsarUid);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Every_attack_refreshes_only_its_actor_and_still_rejects_new_injury_or_bridge_failure(bool unavailable)
    {
        var s=await Setup();factory.Hospital.Enabled=true;
        var path=s.Village.Path+$"/battles/{s.BattleId}/order";
        Assert.Equal(HttpStatusCode.OK,(await s.Residents[0].Client.PutAsJsonAsync(path,new{mode="hold"})).StatusCode);
        Assert.Equal(s.Uids.Take(1),Assert.Single(RequestsFor(s.Uids,s.TsarUid)));
        // This must be a fresh forced check even though the preceding click was healthy.
        if(unavailable)factory.Hospital.Fail=true;
        else {var now=factory.Clock.GetUtcNow().ToUnixTimeSeconds();factory.Hospital.States[s.Uids[0]]=new(s.Uids[0].ToString(),"new-injury",now,now+3600,now,0,0,[]);}
        await AssertRuleBrokenAsync(await s.Residents[0].Client.PutAsJsonAsync(path,new{mode="hold"}),"hospital.unavailable");
        var checks=RequestsFor(s.Uids,s.TsarUid);
        Assert.Equal(2,checks.Length);
        Assert.All(checks,ids=>Assert.Equal(s.Uids.Take(1),ids));
        await using var db=await Db();var others=await db.Citizens.Where(c=>c.VillageId==s.VillageId&&c.Id!=s.Residents[0].Id).ToListAsync();
        Assert.All(others,c=>Assert.Null(c.Recovery));
    }

    [Fact]
    public async Task Ruler_without_resident_row_checks_only_his_own_clinical_state()
    {
        var s=await Setup();factory.Hospital.Enabled=true;
        Assert.Equal(HttpStatusCode.OK,(await s.Village.Tsar.PutAsJsonAsync(s.Village.Path+$"/battles/{s.BattleId}/order",new{mode="hold"})).StatusCode);
        Assert.Equal(new[]{s.TsarUid},Assert.Single(RequestsFor(s.Uids,s.TsarUid)));
    }

    [Theory]
    [InlineData(false, false)][InlineData(false, true)]
    [InlineData(true, false)][InlineData(true, true)]
    public async Task Dungeon_clicks_check_only_the_actor_and_force_fresh_injury_or_failure(bool royal, bool unavailable)
    {
        var s=await Setup();
        await using var db=await Db();
        var account=royal ? (await db.Villages.SingleAsync(v=>v.Id==s.VillageId)).OwnerAccountId
            : (await db.Citizens.SingleAsync(c=>c.Id==s.Residents[0].Id)).AccountId;
        var uid=royal ? s.TsarUid : s.Uids[0];
        factory.Hospital.Enabled=true;
        var game=factory.Services.GetRequiredService<GameService>();
        Assert.True(await game.CheckDungeonHospitalAsync(s.VillageId,account,default));
        Assert.Equal(new[]{uid},Assert.Single(RequestsFor(s.Uids,s.TsarUid)));
        if(unavailable)factory.Hospital.Fail=true;
        else {var now=factory.Clock.GetUtcNow().ToUnixTimeSeconds();factory.Hospital.States[uid]=new(uid.ToString(),"new-dungeon-injury",now,now+3600,now,0,0,[]);}
        Assert.False(await game.CheckDungeonHospitalAsync(s.VillageId,account,default));
        var checks=RequestsFor(s.Uids,s.TsarUid);
        Assert.Equal(2,checks.Length);
        Assert.All(checks,ids=>Assert.Equal(new[]{uid},ids));
        await using var fresh=await Db();
        var untouched=await fresh.Citizens.Where(c=>c.VillageId==s.VillageId&&c.AccountId!=account).ToListAsync();
        Assert.All(untouched,c=>Assert.Null(c.Recovery));
    }

    [Fact]
    public async Task Settlement_releases_the_village_between_each_resident_and_retries_do_not_change_progress()
    {
        var s=await Setup();var now=factory.Clock.GetUtcNow();
        CombatDiagnostics.Record(NullLogger.Instance,s.VillageId,s.BattleId,now,1,0,new(1,1000,1,0,0,0));
        var before=await players.LoadCitizenAsync(s.Residents[0].Id);
        await factory.Services.GetRequiredService<GameService>().SettleVillageAsync(s.VillageId,s.Residents.Select(c=>c.Id).ToArray(),default);
        var timing=Assert.Single(Assert.Single(CombatDiagnostics.Read(s.VillageId)).Operations,x=>x.Operation==nameof(GameService.SettleVillageAsync));
        Assert.Equal(3,timing.Count); // One committed resident can never keep the next two inside its lock hold.
        var after=await players.LoadCitizenAsync(s.Residents[0].Id);Assert.Equal(before.Renown,after.Renown);Assert.Equal(before.StoredEnergy,after.StoredEnergy);
    }

    [Fact]
    public void Operation_history_is_bounded_scoped_and_records_holders_separately_from_waiters()
    {
        var village=Guid.NewGuid();var battle=Guid.NewGuid();var now=factory.Clock.GetUtcNow();
        CombatDiagnostics.Record(NullLogger.Instance,village,battle,now,1,1,new(1,1000,1,0,0,0));
        CombatDiagnostics.Operation(village,"SetBattleOrderAsync",now,12,1000);
        CombatDiagnostics.Operation(village,"RaidVillageAsync",now,1000,5);
        var first=Assert.Single(CombatDiagnostics.Read(village)).Operations;
        Assert.Equal("SetBattleOrderAsync",first[0].Operation);Assert.Equal(1000,first[0].HeldMaxMs);
        Assert.Equal(1000,Assert.Single(first,x=>x.Operation=="RaidVillageAsync").WaitMaxMs);
        CombatDiagnostics.Operation(village,"private value /token",now,1,1);
        CombatDiagnostics.Operation(village,"bad",now,double.NaN,1);
        for(var i=0;i<100;i++)CombatDiagnostics.Operation(village,"method"+i,now,1,1);
        var stats=Assert.Single(CombatDiagnostics.Read(village)).Operations;Assert.True(stats.Count<=32);
        Assert.DoesNotContain(stats,x=>x.Operation.Contains("private")||x.Operation=="bad");
        Assert.Empty(CombatDiagnostics.Read(Guid.NewGuid()));
    }
}
