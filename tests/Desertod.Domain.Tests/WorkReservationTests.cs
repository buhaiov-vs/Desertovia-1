using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class WorkReservationTests
{
    private static (Village, ConstructionSite) Site()
    {
        var village = NewVillage();
        var site = ConstructionRules.Place(village, BuildingKind.WoodPile, 9, 11, Now);
        site.Record(0, "bring-sticks", site.Required(0, "bring-sticks"), Guid.NewGuid());
        site.Record(0, "stack-pile", site.Required(0, "stack-pile") - 1, Guid.NewGuid());
        return (village, site);
    }

    [Fact]
    public void Occupied_work_is_rejected_before_energy_or_cycle_changes()
    {
        var (village, site) = Site();
        var citizen = NewCitizen(village);
        var energy = citizen.StoredEnergy;
        var context = new WorkContext { Village = village, SiteWork = [new(Guid.NewGuid(), site.PlotId, 0, "stack-pile", 1)] };
        Assert.Equal("site.jobBusy", Assert.Throws<GameRuleException>(() => CycleEngine.StartJob(citizen, site.PlotId, 0, "stack-pile", context, Now)).Code);
        Assert.Equal(energy, citizen.StoredEnergy);
        Assert.True(citizen.CycleSettled);
        Assert.Null(citizen.CurrentTaskId);
    }

    [Fact]
    public void Legacy_claim_blocks_new_work_and_paused_claim_has_no_expiry()
    {
        var (village, site) = Site();
        var context = new WorkContext { Village = village, SiteWork = [new(Guid.NewGuid(), site.PlotId, 0, "stack-pile", null)] };
        Assert.Equal(7, context.ReservedWork(site.PlotId, 0, "stack-pile", 7));
        Assert.Equal("site.jobBusy", Assert.Throws<GameRuleException>(() => CycleEngine.StartJob(NewCitizen(village), site.PlotId, 0, "stack-pile", context, Now)).Code);
    }

    [Fact]
    public void Accepted_cycle_claims_only_remaining_work_and_settles_once()
    {
        var (village, site) = Site();
        var citizen = NewCitizen(village);
        var context = WorkContext.For(village);
        CycleEngine.StartJob(citizen, site.PlotId, 0, "stack-pile", context, Now);
        Assert.Equal(1, citizen.ReservedSiteWork);
        var end = citizen.CycleEndsAt!.Value;
        var report = CycleEngine.Settle(citizen, context, end)!;
        Assert.Equal(1, report.ProjectContribution);
        Assert.Null(CycleEngine.Settle(citizen, context, end));
    }

    [Fact]
    public void Positive_energy_below_price_starts_tired_but_exact_price_does_not()
    {
        var (village, site) = Site();
        foreach (var energy in new[] { 0.0, 1.0, 1.99, 2.0, 2.01 })
        {
            var citizen = NewCitizen(village);
            citizen.StoredEnergy = energy;
            citizen.EnergyUpdatedAt = Now;
            CycleEngine.StartJob(citizen, site.PlotId, 0, "stack-pile", WorkContext.For(village), Now);
            Assert.Equal(energy < 2, citizen.CycleTired);
        }
    }
}
