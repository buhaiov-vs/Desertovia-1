using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class RescueDeliveryTests
{
    private static (Village V, Citizen Helper, Citizen Patient) Setup()
    {
        var v = NewVillage();
        v.Structures.Add(new() { Kind = BuildingKind.Hospital, PlotId = "20,15", Level = 1, BuiltAt = Now });
        var helper = NewCitizen(v); var patient = NewCitizen(v);
        helper.IdleSpot = new(15.5, 13.5); patient.IdleSpot = new(16.5, 16.5);
        CombatRules.Injure(patient, TimeSpan.FromMinutes(40), Now, rewardEligible: true);
        return (v, helper, patient);
    }

    [Fact] public void Late_rescue_has_no_eligibility_window_and_uses_actual_route()
    {
        var (v, helper, patient) = Setup();
        var start = Now.AddMinutes(30);
        CycleEngine.StartRescue(helper, patient, WorkContext.For(v), start);
        var journey = Assert.IsType<RescueJourney>(helper.RescueJourney);
        Assert.True(journey.PickupAt > start);
        Assert.True(journey.ArrivesAt > journey.PickupAt);
        Assert.True(journey.ArrivesAt < start.AddMinutes(5));
        Assert.Equal(journey.ArrivesAt, helper.CycleEndsAt);
        Assert.Equal(patient.Id, helper.RescuePatientId);
        Assert.Equal(helper.Id, patient.RescueByCitizenId);
        Assert.Null(patient.HospitalizedAt);
    }

    [Fact] public void Delivery_shortens_only_remaining_time_and_rewards_once()
    {
        var (v, helper, patient) = Setup();
        patient.Carry(ResourceKind.Wood, 3);
        CycleEngine.StartRescue(helper, patient, WorkContext.For(v), Now.AddMinutes(12));
        var arrival = helper.RescueJourney!.ArrivesAt;
        var remaining = patient.InjuredUntil!.Value - arrival;
        var renown = helper.Renown;
        var context = new WorkContext { Village = v, RescuePatient = patient };
        var report = CycleEngine.Settle(helper, context, arrival)!;
        Assert.NotNull(patient.HospitalizedAt);
        Assert.Equal(arrival + TimeSpan.FromTicks((long)(remaining.Ticks * .25)), patient.InjuredUntil);
        Assert.True(helper.Renown >= renown + 2);
        Assert.True(report.PrimaryExperience > 0);
        Assert.Equal(3, patient.Carried(ResourceKind.Wood));
        var healedUntil = patient.InjuredUntil; var paid = helper.Renown;
        Assert.False(RescueRules.Admit(patient, v, arrival.AddSeconds(1), helper));
        Assert.Null(CycleEngine.Settle(helper, context, arrival.AddSeconds(1)));
        Assert.Equal(healedUntil, patient.InjuredUntil); Assert.Equal(paid, helper.Renown);
    }

    [Fact] public void No_hospital_or_early_arrival_never_admits_or_rewards()
    {
        var (v, helper, patient) = Setup();
        Assert.False(RescueRules.Admit(patient, v, Now));
        v.Structures.Clear();
        Assert.Equal("rescue.noHospital", Assert.Throws<GameRuleException>(() => CycleEngine.StartRescue(helper, patient, WorkContext.For(v), Now)).Code);
        Assert.Null(helper.RescuePatientId);
    }

    [Fact] public void Second_helper_is_rejected_and_cancel_releases_patient_without_healing()
    {
        var (v, helper, patient) = Setup(); var other = NewCitizen(v);
        var deadline = patient.InjuredUntil;
        CycleEngine.StartRescue(helper, patient, WorkContext.For(v), Now);
        Assert.Equal("rescue.claimed", Assert.Throws<GameRuleException>(() => CycleEngine.StartRescue(other, patient, WorkContext.For(v), Now)).Code);
        var mid = helper.RescueJourney!.PickupAt.AddSeconds(1);
        RescueRules.Cancel(helper, patient, mid);
        Assert.Null(patient.RescueByCitizenId); Assert.Null(helper.RescuePatientId);
        Assert.Equal(deadline, patient.InjuredUntil); Assert.Null(patient.HospitalizedAt);
        Assert.Equal(helper.IdleSpot, patient.IdleSpot);
    }

    [Fact] public void Recovered_patient_or_destroyed_hospital_has_no_delivery_reward()
    {
        var (v, helper, patient) = Setup();
        CycleEngine.StartRescue(helper, patient, WorkContext.For(v), Now);
        var end = helper.CycleEndsAt!.Value;
        v.Structures[0].Damage = 100;
        var report = CycleEngine.Settle(helper, new() { Village = v, RescuePatient = patient }, end)!;
        Assert.Null(patient.HospitalizedAt);
        Assert.Equal(0, report.PrimaryExperience); Assert.Equal(0, report.RenownGained);
        Assert.False(helper.RescueJourney!.Delivered);
    }

    [Fact] public void Administrative_wound_does_not_mint_rescue_renown()
    {
        var (v, helper, patient) = Setup(); patient.RescueRewardEligible = false;
        CycleEngine.StartRescue(helper, patient, WorkContext.For(v), Now);
        var arrival = helper.RescueJourney!.ArrivesAt;
        Assert.True(RescueRules.Admit(patient, v, arrival, helper));
        Assert.Equal(0, helper.Renown);
        Assert.NotNull(patient.HospitalizedAt);
    }

    [Fact] public void Injury_of_a_moving_carrier_drops_patient_at_actual_battle_position()
    {
        var (v, helper, patient) = Setup();
        CycleEngine.StartRescue(helper, patient, WorkContext.For(v), Now);
        var at = helper.RescueJourney!.PickupAt.AddSeconds(1);
        var position = helper.RescueJourney.PositionAt(at); var deadline=patient.InjuredUntil;
        var raid=new Raid {Enemy=EnemyKind.Zombie,EngineVersion=2,StrikesAt=at};
        var battle=new RaidBattle {Id=raid.Id,VillageId=v.Id,Sector="north",StartsAt=at.AddSeconds(-1),LastTickAt=at.AddSeconds(-1),EndsAt=at.AddMinutes(4)};
        battle.Fighters.Add(new(){CitizenId=helper.Id,X=position.X,Y=position.Y,Health=1,MaximumHealth=40,Active=true,Mode="hold"});
        battle.Units.Add(new(){X=position.X,Y=position.Y,Health=100,MaximumHealth=100,Attack=1000,NextAttackAt=at});
        RaidSimulation.Tick(v,raid,battle,[helper,patient],new HashSet<Guid>{helper.Id},at);
        Assert.True(CombatRules.IsInjured(helper,at));
        Assert.Null(patient.RescueByCitizenId);Assert.Null(helper.RescuePatientId);
        Assert.True(patient.IdleSpot!.Value.DistanceTo(position)<.01);
        Assert.Equal(deadline,patient.InjuredUntil);Assert.Null(patient.HospitalizedAt);
        Assert.False(helper.RescueJourney.Delivered);
    }

    [Fact] public void Hospital_bonus_resumes_paid_work_once_at_the_shortened_deadline()
    {
        var (v, helper, patient) = Setup();
        patient.InjuredUntil=null;patient.RescueRewardEligible=false;
        CycleEngine.Start(patient,Task("chop-wood"),WorkContext.For(v),Now);
        var originalEnd=patient.CycleEndsAt!.Value;
        var wounded=Now.AddSeconds(10);
        CombatRules.Injure(patient,TimeSpan.FromMinutes(40),wounded,rewardEligible:true);
        CycleEngine.StartRescue(helper,patient,WorkContext.For(v),wounded.AddSeconds(5));
        var arrival=helper.RescueJourney!.ArrivesAt;
        CycleEngine.Settle(helper,new(){Village=v,RescuePatient=patient},arrival);
        var discharge=patient.InjuredUntil!.Value;
        Assert.True(HospitalRules.Resume(patient,discharge));
        Assert.Equal(originalEnd+(discharge-wounded),patient.CycleEndsAt);
        Assert.False(HospitalRules.Resume(patient,discharge.AddMinutes(1)));
        Assert.Equal(originalEnd+(discharge-wounded),patient.CycleEndsAt);
    }

    [Fact] public void Rescue_from_other_bank_requires_real_open_bridge_route()
    {
        var (v, helper, patient) = Setup();patient.IdleSpot=new(40.5,17.5);patient.Spot=null;
        Assert.Equal("bridge.closed",Assert.Throws<GameRuleException>(()=>CycleEngine.StartRescue(helper,patient,WorkContext.For(v),Now)).Code);
        v.BridgeOpened=true;
        CycleEngine.StartRescue(helper,patient,WorkContext.For(v),Now);
        var river=helper.RescueJourney!.Steps.Where(p=>p.X>=30&&p.X<34).ToList();
        Assert.NotEmpty(river);
        Assert.All(river,p=>Assert.InRange(p.Y,12,13.999));
    }
}
