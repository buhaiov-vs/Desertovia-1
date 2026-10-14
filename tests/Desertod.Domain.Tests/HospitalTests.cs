using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class HospitalTests
{
    [Fact]
    public void Paused_cycle_keeps_goods_energy_and_only_banks_once_after_discharge()
    {
        var village = NewVillage(); var citizen = NewCitizen(village);
        var context = WorkContext.For(village);
        CycleEngine.Start(citizen, Task("chop-wood"), context, Now);
        var originalEnd = citizen.CycleEndsAt!.Value;
        var energy = citizen.StoredEnergy;
        citizen.Carry(ResourceKind.Leather, 2);
        var admitted = Now.AddSeconds(10);
        var until = Now.AddHours(24);
        HospitalRules.Apply(citizen, new ExternalRecovery { Available=true, EpisodeId="duel-a", AdmittedAt=admitted, Until=until }, admitted);
        Assert.Equal(WorkState.Paused, citizen.WorkState(Now.AddHours(1)));
        Assert.Null(CycleEngine.Settle(citizen, context, originalEnd.AddMinutes(1)));
        Assert.False(citizen.CycleSettled);
        Assert.Equal(energy, citizen.StoredEnergy);
        Assert.Equal(2, citizen.Carried(ResourceKind.Leather));
        Assert.Throws<GameRuleException>(() => CycleEngine.Cancel(citizen, admitted, village));
        Assert.Throws<GameRuleException>(() => CycleEngine.Start(citizen, Task("chop-wood"), context, admitted));
        // A repeated status must not move the pause boundary.
        HospitalRules.Apply(citizen, new ExternalRecovery { Available=true, EpisodeId="duel-a", AdmittedAt=admitted, Until=until }, Now.AddHours(2));
        HospitalRules.Apply(citizen, new ExternalRecovery { Available=true }, until);
        var expectedEnd = originalEnd + (until-admitted);
        Assert.Equal(expectedEnd, citizen.CycleEndsAt);
        Assert.Null(CycleEngine.Settle(citizen, context, expectedEnd.AddSeconds(-1)));
        Assert.NotNull(CycleEngine.Settle(citizen, context, expectedEnd));
        Assert.Null(CycleEngine.Settle(citizen, context, expectedEnd.AddSeconds(1)));
        Assert.Equal(2, citizen.Carried(ResourceKind.Leather));
    }

    [Fact]
    public void Remedy_does_not_clear_duel_and_discharge_does_not_clear_native_injury()
    {
        var village=NewVillage();var citizen=NewCitizen(village);
        citizen.InjuredUntil=Now.AddHours(2);citizen.Carry(ResourceKind.Remedy,1);
        HospitalRules.Apply(citizen,new ExternalRecovery{Available=true,Until=Now.AddHours(24)},Now);
        CombatRules.Heal(citizen,village,Now);
        Assert.True(HospitalRules.IsAdmitted(citizen,Now));
        citizen.InjuredUntil=Now.AddHours(3);
        HospitalRules.Apply(citizen,new ExternalRecovery{Available=true},Now.AddHours(1));
        Assert.Equal(Now.AddHours(3),citizen.InjuredUntil);
    }

    [Fact]
    public void Unavailable_bridge_never_discards_a_known_wound_or_banks_work()
    {
        var village=NewVillage();var citizen=NewCitizen(village);
        CycleEngine.Start(citizen,Task("chop-wood"),WorkContext.For(village),Now);
        HospitalRules.Apply(citizen,new ExternalRecovery{Available=false,Until=Now.AddHours(24)},Now);
        Assert.Throws<GameRuleException>(()=>HospitalRules.RequireReady(citizen,Now.AddHours(25)));
        Assert.Null(CycleEngine.Settle(citizen,WorkContext.For(village),Now.AddHours(25)));
    }

    [Fact]
    public void Hospital_upgrade_is_collective_slow_construction_and_bounded()
    {
        var village=NewVillage();
        village.Structures.Add(new Structure{Kind=BuildingKind.Hospital,PlotId="20,15",BuiltAt=Now,Level=1});
        var site=ConstructionRules.UpgradeHospital(village,"20,15",Now);
        Assert.Equal(2,site.TargetLevel);Assert.Equal(1,HospitalRules.Level(village));
        Assert.True(site.Required(1,"bring-stones")>=96);
        Assert.Throws<GameRuleException>(()=>ConstructionRules.UpgradeHospital(village,"20,15",Now));
        village.Sites.Clear();village.Structures[0].Level=3;
        Assert.Throws<GameRuleException>(()=>ConstructionRules.UpgradeHospital(village,"20,15",Now));
    }

    [Fact]
    public void Hospitalized_guard_does_not_fight_raids()
    {
        var village=NewVillage();var citizen=NewCitizen(village);
        citizen.CurrentTaskId="keep-watch";citizen.CycleSettled=false;
        citizen.CycleStartedAt=Now;citizen.CycleEndsAt=Now.AddHours(1);
        citizen.Recovery=new ExternalRecovery{Available=true,Until=Now.AddHours(24)};
        Assert.Null(RaidRules.GuardTask(citizen,Now.AddMinutes(1)));
    }
}
