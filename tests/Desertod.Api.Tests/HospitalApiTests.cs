using System.Net;
using System.Net.Http.Json;
using Desertod.Api.Game;
using Desertod.Domain.Entities;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Desertod.Api.Tests.Players;

namespace Desertod.Api.Tests;

public sealed class TestHospitalBridge : IHospitalBridge
{
    public bool Enabled { get; set; }
    public bool Fail { get; set; }
    public Dictionary<long, HospitalPatient> States { get; } = [];
    public System.Collections.Concurrent.ConcurrentQueue<long[]> StatusRequests { get; } = new();
    public (long Actor, long Patient, HospitalHealRequest Request)? Payment { get; set; }
    public Task<HospitalStatusReply> StatusAsync(long actor, IReadOnlyList<HospitalPatientRequest> patients, CancellationToken ct)
    {
        StatusRequests.Enqueue(patients.Select(p=>p.Id).ToArray());
        if (Fail) throw new HttpRequestException();
        IReadOnlyList<HospitalTier> tiers = [new("candle", "Candle", 1, 2, 50, "")];
        return Task.FromResult(new HospitalStatusReply(patients.Select(p => States.GetValueOrDefault(p.Id)
            ?? new HospitalPatient(p.Id.ToString(), null, null, null, 0, 0, 0, tiers)).ToList(), 200));
    }
    public Task<HospitalTreatmentReply> HealAsync(long actor, long patient, HospitalHealRequest request, CancellationToken ct)
    {
        Payment = (actor, patient, request);
        States.Remove(patient);
        return Task.FromResult(new HospitalTreatmentReply("completed", null, null, request.ExpectedPrice, 3600));
    }
    public void Reset() { Enabled=false;Fail=false;States.Clear();Payment=null;StatusRequests.Clear(); }
}

[Collection(ApiCollection.Name)]
public sealed class HospitalApiTests(DesertodApiFactory factory) : IDisposable
{
    private readonly Players players = new(factory);
    private static long nextUid = 42000;
    private readonly long uid = Interlocked.Increment(ref nextUid);
    public void Dispose() => factory.Hospital.Reset();
    private async Task<(FoundedVillage Village, Resident Citizen)> SetupAsync()
    {
        factory.Hospital.Reset();
        var village=await players.FoundVillageAsync("Hospital");var citizen=await players.JoinAsync(village);
        await using var db=await factory.Services.GetRequiredService<IDbContextFactory<DesertodDbContext>>().CreateDbContextAsync();
        var stored=await db.Citizens.SingleAsync(c=>c.Id==citizen.Id);
        db.ExternalLogins.Add(new ExternalLogin{Provider="telegram",ProviderUserId=uid.ToString(),AccountId=stored.AccountId});
        await db.SaveChangesAsync();factory.Hospital.Enabled=true;
        return(village,citizen);
    }
    private void Wound()
    {
        var now=factory.Clock.GetUtcNow().ToUnixTimeSeconds();
        factory.Hospital.States[uid]=new HospitalPatient(uid.ToString(),"episode",now,now+86400,now,4,2,[new("candle","Candle",1,2,50,"")]);
    }
    [Fact]
    public async Task Duel_state_blocks_direct_work_walk_and_inventory_but_keeps_view_available()
    {
        var (_,citizen)=await SetupAsync();
        await OkJsonAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/task",new{taskId="chop-wood"}));
        var before=await players.LoadCitizenAsync(citizen.Id);Wound();
        var hospital=await OkJsonAsync(await citizen.Client.GetAsync($"{citizen.Path}/hospital"));
        Assert.True(hospital.GetProperty("available").GetBoolean());
        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/task",new{taskId="chop-wood"}),"hospital.admitted");
        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/goto",new{spot=new{x=20,y=20}}),"hospital.admitted");
        await AssertRuleBrokenAsync(await citizen.Client.DeleteAsync($"{citizen.Path}/me/task"),"hospital.admitted");
        var view=await OkJsonAsync(await citizen.Client.GetAsync($"{citizen.Path}/me"));
        Assert.Equal("Paused",view.GetProperty("state").GetString());
        var after=await players.LoadCitizenAsync(citizen.Id);
        Assert.Equal(before.CurrentTaskId,after.CurrentTaskId);Assert.False(after.CycleSettled);
    }
    [Fact]
    public async Task Healing_resolves_payer_from_linked_session_and_refuses_other_village_patient()
    {
        var (_,citizen)=await SetupAsync();Wound();
        var request=new{patientId=citizen.Id,code="candle",requestId=Guid.NewGuid().ToString(),expectedPrice=50,episodeId="episode",actor=99999};
        var result=await OkJsonAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/hospital/heal",request));
        Assert.Equal("completed",result.GetProperty("state").GetString());
        Assert.Equal(uid,factory.Hospital.Payment!.Value.Actor);Assert.Equal(uid,factory.Hospital.Payment.Value.Patient);
        var other=await players.FoundVillageAsync("Other Hospital");var stranger=await players.JoinAsync(other);
        var response=await citizen.Client.PostAsJsonAsync($"{citizen.Path}/hospital/heal",new{patientId=stranger.Id,code="candle",requestId=Guid.NewGuid().ToString(),expectedPrice=50,episodeId="episode"});
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }
    [Fact]
    public async Task Lost_bridge_keeps_known_wound_and_pauses_instead_of_granting_rewards()
    {
        var (_,citizen)=await SetupAsync();Wound();
        await OkJsonAsync(await citizen.Client.GetAsync($"{citizen.Path}/hospital"));factory.Hospital.Fail=true;
        var result=await OkJsonAsync(await citizen.Client.GetAsync($"{citizen.Path}/hospital"));
        Assert.False(result.GetProperty("available").GetBoolean());
        var stored=await players.LoadCitizenAsync(citizen.Id);Assert.NotNull(stored.Recovery!.Until);
        await AssertRuleBrokenAsync(await citizen.Client.PostAsJsonAsync($"{citizen.Path}/me/task",new{taskId="chop-wood"}),"hospital.unavailable");
    }
}
