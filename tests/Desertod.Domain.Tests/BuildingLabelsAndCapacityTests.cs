using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public sealed class BuildingLabelsAndCapacityTests
{
    [Fact]
    public void Types_start_at_one_without_changing_the_existing_global_identity()
    {
        var v=Village.Create("labels","Labels",Guid.NewGuid(),Now);
        v.Structures=[new(){Kind=BuildingKind.Hut,PlotId="0,0",Number=10},
            new(){Kind=BuildingKind.Library,PlotId="4,0",Number=11},new(){Kind=BuildingKind.Hut,PlotId="8,0",Number=12}];
        v.Storages=[new(){Kind=StorageKind.WoodPile,PlotId="0,4",Number=13},new(){Kind=StorageKind.Woodyard,PlotId="4,4",Number=14}];
        v.BuildingSequence=20;BuildingNumbers.Ensure(v);
        var visible=BuildingLabels.Visible(v);
        Assert.Equal(1,visible[10]);Assert.Equal(2,visible[12]);Assert.Equal(0,visible[11]);
        Assert.Equal(1,visible[13]);Assert.Equal(2,visible[14]);Assert.Equal(20,v.BuildingSequence);
        Assert.Equal(new[]{10,11,12},v.Structures.Select(s=>s.Number));
    }

    [Fact]
    public void Demolition_never_reuses_a_number_and_upgrades_share_the_same_visible_identity()
    {
        var v=Village.Create("labels","Labels",Guid.NewGuid(),Now);
        v.Structures=[new(){Kind=BuildingKind.Hut,PlotId="0,0"},new(){Kind=BuildingKind.Hut,PlotId="4,0"}];
        BuildingNumbers.Ensure(v);var first=v.Structures[0];var second=v.Structures[1];
        v.Structures.Remove(first);BuildingNumbers.Ensure(v);
        Assert.Equal(0,BuildingLabels.Visible(v)[second.Number]);
        v.Structures.Add(new(){Kind=BuildingKind.Hut,PlotId="8,0"});BuildingNumbers.Ensure(v);
        Assert.Equal(2,BuildingLabels.Visible(v)[second.Number]);Assert.Equal(3,BuildingLabels.Visible(v)[v.Structures[^1].Number]);
        v.Sites.Add(new(){Kind=second.Kind,PlotId=second.PlotId,Operation="housing-upgrade",TargetLevel=2});
        BuildingNumbers.Ensure(v);Assert.Equal(second.Number,v.Sites.Single().Number);
        Assert.Equal(2,BuildingLabels.Visible(v)[second.Number]);
        BuildingOperations.Finish(v,v.Sites.Single(),Now);
        Assert.Equal(2,second.Level);Assert.Equal(2,BuildingLabels.Visible(v)[second.Number]);
    }

    [Fact]
    public void Dense_housing_upgrades_increase_beds_through_real_construction_without_repairing_old_damage()
    {
        var v=NewVillage();var c=NewCitizen(v);
        var hut=new Structure{Kind=BuildingKind.Hut,PlotId="0,10",Level=1,Damage=40};v.Structures.Add(hut);
        Assert.Equal(4,ShelterRules.Beds(hut));
        var order=BuildingOperations.Request(v,v.OwnerAccountId,hut.PlotId,"upgrade",Now);
        BuildingOperations.Approve(v,order,[c],Now);
        var site=Assert.Single(v.Sites);Assert.Equal("housing-upgrade",site.Operation);Assert.Equal(1,hut.Level);
        Assert.NotEmpty(site.Definition.Stages.Single().Jobs);
        Assert.False(site.IsFinished);site.Stage=site.Definition.Stages.Count;
        BuildingOperations.Finish(v,site,Now);
        Assert.Equal(2,hut.Level);Assert.Equal(6,ShelterRules.Beds(hut));Assert.Equal(40,hut.Damage);
        hut.Level=3;Assert.Equal(8,ShelterRules.Beds(hut));
        Assert.Equal(36,ShelterRules.Beds(new(){Kind=BuildingKind.Longhouse,PlotId="10,10",Level=3}));
    }

    [Fact]
    public void Expanded_resource_bins_keep_old_goods_and_accept_only_their_resource_types()
    {
        var store=StorageBuilding.Create(StorageKind.WoodPile,"0,0",Now);
        store.Put(ResourceKind.Wood,30);store.Kind=StorageLimits.Upgrade(store.Kind)!.Value;
        Assert.Equal(30,store.Amount(ResourceKind.Wood));Assert.Equal(90,store.FreeSpace(ResourceKind.Wood));
        Assert.Equal(0,store.Put(ResourceKind.Stone,20));Assert.Equal(90,store.Put(ResourceKind.Wood,200));
        Assert.Equal(120,store.Amount(ResourceKind.Wood));
        Assert.True(KingdomRules.AreaForLevel(3).Width>KingdomRules.AreaForLevel(1).Width);
    }
    [Theory]
    [InlineData(15)][InlineData(30)][InlineData(36)]
    public void Meals_cost_one_food_per_three_completed_cycles_and_idle_time_does_not_charge_more(int cycles)
    {
        var v=NewVillage();var c=NewCitizen(v);var context=Desertod.Domain.Rules.WorkContext.For(v);
        var before=v.Stock(ResourceKind.Food);
        for(var cycle=0;cycle<cycles;cycle++)
        {
            Desertod.Domain.Rules.CycleEngine.Start(c,Task("quarry-stone"),context,c.CycleEndsAt??Now);
            Finish(c,context);
        }
        Assert.Equal(cycles/3,before-v.Stock(ResourceKind.Food));Assert.False(c.IsHungry);
        var after=v.Stock(ResourceKind.Food);
        Assert.Null(Desertod.Domain.Rules.CycleEngine.Settle(c,context,c.CycleEndsAt!.Value.AddDays(1)));
        Assert.Equal(after,v.Stock(ResourceKind.Food));
    }

}
