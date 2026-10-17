using Desertod.Domain.Buildings;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using static Desertod.Domain.Tests.TestWorld;
namespace Desertod.Domain.Tests;
public sealed class UpgradeAvailabilityTests
{
    [Theory]
    [InlineData(BuildingKind.Library,1,"building.upgradeUnsupported")]
    [InlineData(BuildingKind.Hut,3,"building.maxLevel")]
    [InlineData(BuildingKind.Hospital,1,"building.hospitalUpgrade")]
    [InlineData(BuildingKind.Hospital,3,"building.maxLevel")]
    [InlineData(BuildingKind.Longhouse,3,"building.maxLevel")]
    [InlineData(BuildingKind.Castle,3,"building.maxLevel")]
    public void Upgrade_error_distinguishes_unsupported_type_from_maximum_level(BuildingKind kind,int level,string code)
    {
        var v=NewVillage();v.Structures.Add(new(){Kind=kind,Level=level,PlotId="1,1"});
        Assert.Equal(code,Assert.Throws<GameRuleException>(()=>BuildingOperations.Request(v,Guid.NewGuid(),"1,1","upgrade",Now)).Code);
        Assert.Empty(v.CivicOrders);Assert.Empty(v.Sites);
    }
    [Theory]
    [InlineData(StorageKind.Woodyard)]
    [InlineData(StorageKind.StoneYard)]
    [InlineData(StorageKind.Granary)]
    public void Upgraded_storage_reports_maximum_level(StorageKind kind)
    {
        var v=NewVillage();v.Storages.Add(StorageBuilding.Create(kind,"1,1",Now));
        Assert.Equal("building.maxLevel",Assert.Throws<GameRuleException>(()=>BuildingOperations.Request(v,Guid.NewGuid(),"1,1","upgrade",Now)).Code);
    }
    [Fact]
    public void A_stale_request_is_revalidated_and_does_not_create_another_upgrade()
    {
        var v=NewVillage();var storage=StorageBuilding.Create(StorageKind.WoodPile,"1,1",Now);v.Storages.Add(storage);
        var order=BuildingOperations.Request(v,Guid.NewGuid(),"1,1","upgrade",Now);
        storage.Kind=StorageKind.Woodyard;
        Assert.Equal("building.maxLevel",Assert.Throws<GameRuleException>(()=>BuildingOperations.Approve(v,order,[],Now)).Code);
        Assert.Equal("pending",order.Status);Assert.Empty(v.Sites);
    }
}
