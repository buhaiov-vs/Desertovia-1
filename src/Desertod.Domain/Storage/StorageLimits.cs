using Desertod.Domain.Catalog;
namespace Desertod.Domain.Storage;

/// <summary>Fixed resource bins. Legacy overflow stays withdrawable; deposits never erase it.</summary>
public static class StorageLimits
{
    public static int PerResource(StorageKind kind) => kind switch
    {
        StorageKind.Heap => 10,
        StorageKind.WoodPile or StorageKind.StonePile or StorageKind.DryingRack => 30,
        StorageKind.Woodyard or StorageKind.StoneYard or StorageKind.Granary => 120,
        StorageKind.Treasury => 100,
        StorageKind.GreatGranary => 240,
        _ => 20,
    };
    public static int? Total(StorageKind kind) => kind switch
    { StorageKind.Heap => 40, StorageKind.TownHall or StorageKind.Storehouse => 120, _ => null };
    public static StorageKind? Upgrade(StorageKind kind) => kind switch
    { StorageKind.WoodPile => StorageKind.Woodyard, StorageKind.StonePile => StorageKind.StoneYard,
      StorageKind.DryingRack => StorageKind.Granary, _ => null };
}
