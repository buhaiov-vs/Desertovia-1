using Desertod.Domain.Buildings;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using static Desertod.Domain.Tests.TestWorld;
namespace Desertod.Domain.Tests;

public sealed class CivicRequestCapacityTests
{
    [Theory]
    [InlineData("build")]
    [InlineData("upgrade")]
    [InlineData("demolish")]
    public void Full_public_queue_rejects_request_before_creating_an_order(string operation)
    {
        var village=NewVillage();var actor=Guid.NewGuid();
        village.Storages.Add(StorageBuilding.Create(StorageKind.WoodPile,"1,1",Now));
        foreach(var plot in new[]{"9,11","21,12","25,12"}) village.Sites.Add(new(){Kind=BuildingKind.LeanTo,PlotId=plot});
        var trees=village.Trees.Select(t=>t.FelledAt).ToArray();
        var error=Assert.Throws<GameRuleException>(()=> {
            if(operation=="build")BuildingOperations.RequestBuild(village,actor,BuildingKind.Hospital,9,17,Now);
            else BuildingOperations.Request(village,actor,"1,1",operation,Now);
        });
        Assert.Equal("building.requestQueueFull",error.Code);Assert.Empty(village.CivicOrders);Assert.Equal(3,village.Sites.Count);
        Assert.Equal(trees,village.Trees.Select(t=>t.FelledAt).ToArray());
    }

    [Fact]
    public void Pending_requests_share_capacity_and_rejection_frees_a_place_without_blocking_approval()
    {
        var village=NewVillage();var actor=Guid.NewGuid();
        village.Storages.Add(StorageBuilding.Create(StorageKind.WoodPile,"1,1",Now));
        foreach(var plot in new[]{"9,11","21,12"})village.Sites.Add(new(){Kind=BuildingKind.LeanTo,PlotId=plot});
        var order=BuildingOperations.RequestBuild(village,actor,BuildingKind.Hospital,9,17,Now);
        Assert.Equal("building.requestQueueFull",Assert.Throws<GameRuleException>(()=>BuildingOperations.Request(village,actor,"1,1","upgrade",Now)).Code);
        order.Status="rejected";
        var upgrade=BuildingOperations.Request(village,actor,"1,1","upgrade",Now);
        BuildingOperations.Approve(village,upgrade,[],Now);
        Assert.Equal("working",upgrade.Status);Assert.Equal(3,village.Sites.Count);
        Assert.Equal("building.requestQueueFull",Assert.Throws<GameRuleException>(()=>BuildingOperations.RequestBuild(village,actor,BuildingKind.Hospital,9,17,Now)).Code);
    }

    [Fact]
    public void Public_queue_and_housing_requests_do_not_consume_an_owners_private_slot()
    {
        var village=NewVillage();var actor=Guid.NewGuid();
        foreach(var plot in new[]{"9,11","21,12","25,12"})village.Sites.Add(new(){Kind=BuildingKind.LeanTo,PlotId=plot});
        var storage=StorageBuilding.Create(StorageKind.WoodPile,"1,1",Now);storage.OwnerAccountId=actor;village.Storages.Add(storage);
        village.CivicOrders.Add(new(){PlotId="housing",Operation="housing",RequestedBy=actor});
        var order=BuildingOperations.Request(village,actor,"1,1","upgrade",Now);
        Assert.Equal("pending",order.Status);
        var other=StorageBuilding.Create(StorageKind.WoodPile,"5,1",Now);other.OwnerAccountId=actor;village.Storages.Add(other);
        Assert.Equal("building.requestQueueFull",Assert.Throws<GameRuleException>(()=>BuildingOperations.Request(village,actor,"5,1","upgrade",Now)).Code);
    }
}
