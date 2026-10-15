using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Storage;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public class StorageTests
{
    private static readonly DateTimeOffset Now = TestWorld.Now;
    private const string HeapPlot = "17,12";

    /// <summary>A new tribe that has built its Heap.</summary>
    private static Village NewCamp()
    {
        var village = Village.Create("v", "V", Guid.NewGuid(), Now);
        village.Storages.Add(StorageBuilding.Create(StorageKind.Heap, HeapPlot, Now));
        return village;
    }

    [Fact]
    public void NewTribe_HasNoStorage()
    {
        var village = Village.Create("v", "V", Guid.NewGuid(), Now);

        Assert.Empty(village.Storages);
        Assert.Empty(village.Structures);
        Assert.Empty(village.Sites);
        Assert.Equal(0, village.FreeSpace(ResourceKind.Wood));
        Assert.Equal(WorldMap.Places[WorldMap.Camp], village.DropPoint(ResourceKind.Wood, WorldMap.Places["forest"]));
    }

    [Fact]
    public void WithoutStorage_CarryingToStorageHasNowhereToGo()
    {
        var village = Village.Create("v", "V", Guid.NewGuid(), Now);
        var citizen = TestWorld.NewCitizen(village);
        citizen.Carry(ResourceKind.Wood, 4);

        Assert.Equal("hands.noRoom",
            Assert.Throws<GameRuleException>(() =>
                CycleEngine.Start(citizen, TestWorld.Task(GameCatalog.CarryToStorage), WorkContext.For(village), Now)).Code);
        Assert.Equal(4, citizen.Carried(ResourceKind.Wood));
    }

    [Fact]
    public void Heap_UsesFixedResourceBins()
    {
        var village = NewCamp();
        foreach (var kind in new[] { ResourceKind.Wood, ResourceKind.Stone, ResourceKind.Food, ResourceKind.Reeds })
        {
            village.Store(kind, 1);
        }

        Assert.Equal(10, village.FreeSpace(ResourceKind.Leather));
        Assert.Equal(1, village.Store(ResourceKind.Leather, 1));
        Assert.Equal(9, village.FreeSpace(ResourceKind.Wood));
    }

    [Fact]
    public void Store_FillsSpecialisedBuildingsBeforeTheHeap()
    {
        var village = NewCamp();
        village.Storages.Add(StorageBuilding.Create(StorageKind.WoodPile, "13,5", Now));

        village.Store(ResourceKind.Wood, 30);

        Assert.Equal(30, village.Storages.Single(s => s.Kind == StorageKind.WoodPile).Amount(ResourceKind.Wood));
        Assert.Equal(0, village.Storages.Single(s => s.Kind == StorageKind.Heap).Amount(ResourceKind.Wood));
    }

    [Fact]
    public void Take_EmptiesTheHeapFirst()
    {
        var village = NewCamp();
        village.Store(ResourceKind.Wood, 10);
        village.Storages.Add(StorageBuilding.Create(StorageKind.WoodPile, "13,5", Now));
        village.Store(ResourceKind.Wood, 10);

        village.Take(ResourceKind.Wood, 10);

        Assert.Equal(0, village.Storages.Single(s => s.Kind == StorageKind.Heap).Amount(ResourceKind.Wood));
        Assert.Equal(10, village.Stock(ResourceKind.Wood));
    }

    [Fact]
    public void Knowledge_IsUnlimited_AndNotStoredInBuildings()
    {
        var village = NewCamp();

        Assert.Equal(5000, village.Store(ResourceKind.ResearchPoints, 5000));
        Assert.Equal(5000, village.Knowledge);
        Assert.Empty(village.Storages.Single().Goods);
    }

    [Fact]
    public void DropPoint_IsTheNearestBuildingWithRoom()
    {
        var village = NewCamp();
        var forest = WorldMap.Places["forest"];
        Assert.Equal(WorldMap.Yard(HeapPlot), village.DropPoint(ResourceKind.Wood, forest));

        village.Storages.Add(StorageBuilding.Create(StorageKind.WoodPile, "9,11", Now));
        Assert.Equal(WorldMap.Yard("9,11"), village.DropPoint(ResourceKind.Wood, forest));

        village.Store(ResourceKind.Wood, 30);
        Assert.Equal(WorldMap.Yard(HeapPlot), village.DropPoint(ResourceKind.Wood, forest));
    }

    [Fact]
    public void Take_MovesGoodsFromOneBuildingIntoHands_UpToTheLimit()
    {
        var village = NewCamp();
        village.Store(ResourceKind.Food, 10);
        village.Storages.Add(StorageBuilding.Create(StorageKind.Granary, "13,5", Now));
        village.Store(ResourceKind.Food, 30);
        var citizen = TestWorld.NewCitizen(village);

        Assert.Equal(StorageRules.TakeLimit, StorageRules.Take(citizen, village, "13,5", ResourceKind.Food, 50, Now));
        Assert.Equal(StorageRules.TakeLimit, citizen.Carried(ResourceKind.Food));
        Assert.Equal(10, village.Storages.Single(s => s.Kind == StorageKind.Heap).Amount(ResourceKind.Food));

        Assert.Equal("take.handsFull",
            Assert.Throws<GameRuleException>(() => StorageRules.Take(citizen, village, "13,5", ResourceKind.Food, 1, Now)).Code);
        Assert.Equal("take.nothing",
            Assert.Throws<GameRuleException>(() => StorageRules.Take(citizen, village, "13,5", ResourceKind.Wood, 1, Now)).Code);
        Assert.Equal("take.noStorage",
            Assert.Throws<GameRuleException>(() => StorageRules.Take(citizen, village, "13,18", ResourceKind.Wood, 1, Now)).Code);
    }

    [Fact]
    public void Take_TakesTheAmountAsked()
    {
        var village = NewCamp();
        village.Store(ResourceKind.Food, 10);
        var citizen = TestWorld.NewCitizen(village);

        Assert.Equal(3, StorageRules.Take(citizen, village, HeapPlot, ResourceKind.Food, 3, Now));
        Assert.Equal(3, citizen.Carried(ResourceKind.Food));
        Assert.Equal(7, village.Stock(ResourceKind.Food));

        Assert.Equal(StorageRules.TakeLimit - 3, StorageRules.Take(citizen, village, HeapPlot, ResourceKind.Food, 9, Now));
        Assert.Equal("take.amount",
            Assert.Throws<GameRuleException>(() => StorageRules.Take(citizen, village, HeapPlot, ResourceKind.Food, 0, Now)).Code);
    }

    [Fact]
    public void Take_NeedsAnIdleCitizen()
    {
        var village = TestWorld.NewVillage();
        var citizen = TestWorld.NewCitizen(village);
        CycleEngine.Start(citizen, TestWorld.Task("chop-wood"), WorkContext.For(village), Now);

        Assert.Equal("take.working",
            Assert.Throws<GameRuleException>(() => StorageRules.Take(citizen, village, "17,5", ResourceKind.Food, 1, Now)).Code);
    }

    [Fact]
    public void Drop_ThrowsAwayOnlyGoodsStorageHasNoRoomFor()
    {
        var village = NewCamp();
        village.Store(ResourceKind.Wood, 30);
        var citizen = TestWorld.NewCitizen(village);
        citizen.Carry(ResourceKind.Food, 5);

        Assert.Equal("drop.roomInStorage",
            Assert.Throws<GameRuleException>(() => StorageRules.Drop(citizen, village)).Code);

        // Fill the stone bin, independently of the food bin.
        village.Store(ResourceKind.Stone,10);
        village.Store(ResourceKind.Food, 5);
        citizen.Carry(ResourceKind.Stone, 3);
        StorageRules.Drop(citizen, village);

        Assert.Equal(0, citizen.Carried(ResourceKind.Stone));
        Assert.Equal(5, citizen.Carried(ResourceKind.Food));
    }
}
