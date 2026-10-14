using Desertod.Domain.Catalog;

namespace Desertod.Domain.Farming;

public enum CropKind
{
    Turnip,
    Carrot,
    Cabbage,
    Flax,
    Rye,
}

/// <param name="SkillLevel">Farming level needed to sow it.</param>
/// <param name="GrowHours">Time to ripen in a bed that is kept watered.</param>
/// <param name="Harvest">Goods of a ripe bed at a stat factor of 1.</param>
public sealed record CropDefinition(CropKind Kind, string Name, int SkillLevel, double GrowHours, IReadOnlyList<ResourceAmount> Harvest);

public static class FarmCatalog
{
    public const int BedsPerFarm = 3;

    /// <summary>A dry bed grows at this share of the speed of a watered one.</summary>
    public const double DryGrowthRate = 0.25;

    /// <summary>Beds one watering reaches, the thirstiest first.</summary>
    public const int BedsPerWatering = 3;

    /// <summary>How long a bed stays wet after watering.</summary>
    public static TimeSpan WaterDuration { get; } = TimeSpan.FromMinutes(30);

    /// <summary>A growing bed that stays wet for less than this needs water.</summary>
    public static TimeSpan ThirstyBelow { get; } = TimeSpan.FromMinutes(15);

    public static IReadOnlyList<CropDefinition> Crops { get; } =
    [
        new(CropKind.Turnip, "turnips", 0, 1, [new(ResourceKind.Food, 15)]),
        new(CropKind.Carrot, "carrots", 2, 1.5, [new(ResourceKind.Food, 24)]),
        new(CropKind.Cabbage, "cabbage", 3, 2, [new(ResourceKind.Food, 36)]),
        new(CropKind.Flax, "flax", 4, 2, [new(ResourceKind.Flax, 10)]),
        new(CropKind.Rye, "rye", 5, 3, [new(ResourceKind.Grain, 20), new(ResourceKind.Food, 10)]),
    ];

    private static readonly Dictionary<CropKind, CropDefinition> ByKind = Crops.ToDictionary(c => c.Kind);

    public static CropDefinition Get(CropKind kind) => ByKind[kind];
}
