using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Weather;
using Desertod.Domain.World;

namespace Desertod.Domain.Farming;

/// <summary>
/// Beds on the village farms. A citizen sows a crop in an empty bed, it grows by itself, faster while the bed is wet,
/// and anyone can harvest it once it is ripe. Watering needs no skill; better seeds need more Endurance.
/// </summary>
public static class FarmRules
{
    private const double Epsilon = 1e-6;

    /// <summary>Gives every farm its beds, also farms built before beds existed. Returns true if any bed was added.</summary>
    public static bool EnsureBeds(Village village)
    {
        var added = false;
        foreach (var farm in village.Structures.Where(s => s.Kind == BuildingKind.Farm))
        {
            for (var index = 0; index < FarmCatalog.BedsPerFarm; index++)
            {
                if (village.Fields.Any(b => b.PlotId == farm.PlotId && b.Index == index))
                {
                    continue;
                }

                village.Fields.Add(new FieldBed { PlotId = farm.PlotId, Index = index, UpdatedAt = farm.BuiltAt });
                added = true;
            }
        }

        return added;
    }

    /// <summary>Rain waters every bed while it falls.</summary>
    public static double GrowthOf(Village village, FieldBed bed, DateTimeOffset now)
    {
        if (bed.Crop is not { } crop)
        {
            return 0;
        }

        var hours = Math.Max(0, (now - bed.UpdatedAt).TotalHours);
        var wetHours = Math.Min(hours, WeatherRules.WetHours(village, bed.UpdatedAt, now, bed.WateredUntil));
        var grown = (wetHours + (hours - wetHours) * FarmCatalog.DryGrowthRate) / FarmCatalog.Get(crop).GrowHours;
        return Math.Min(1, bed.Growth + grown);
    }

    public static bool IsRipe(Village village, FieldBed bed, DateTimeOffset now) => bed.Crop is not null && GrowthOf(village, bed, now) >= 1 - Epsilon;

    public static bool IsWet(FieldBed bed, DateTimeOffset now) => bed.WateredUntil > now;

    /// <summary>A growing bed that dries out soon. Empty and ripe beds need no water, and none do in the rain.</summary>
    public static bool IsThirsty(Village village, FieldBed bed, DateTimeOffset now) =>
        bed.Crop is not null && !IsRipe(village, bed, now) && (bed.WateredUntil ?? DateTimeOffset.MinValue) - now < FarmCatalog.ThirstyBelow
        && !WeatherRules.IsRaining(village, now);

    public static FieldBed? EmptyBed(Village village, Guid? owner = null) => Beds(village, owner).FirstOrDefault(b => b.Crop is null);

    public static FieldBed? RipeBed(Village village, DateTimeOffset now, Guid? owner = null) => Beds(village, owner).FirstOrDefault(b => IsRipe(village, b, now));

    /// <summary>Thirsty beds, driest first.</summary>
    public static IEnumerable<FieldBed> ThirstyBeds(Village village, DateTimeOffset now, Guid? owner = null) =>
        Beds(village, owner).Where(b => IsThirsty(village, b, now)).OrderBy(b => b.WateredUntil ?? DateTimeOffset.MinValue);

    /// <summary>Checks that the work has a bed to do it on. Throws a rule error explaining why not.</summary>
    public static void Check(Village village, TaskDefinition task, DateTimeOffset now, Guid? owner = null)
    {
        EnsureBeds(village);
        switch (task.Effect)
        {
            case TaskEffect.Sow when EmptyBed(village, owner) is null:
                throw new GameRuleException("farm.noEmptyBed", "Every bed is sown. Harvest a ripe one first.");
            case TaskEffect.Water when WeatherRules.IsRaining(village, now):
                throw new GameRuleException("farm.raining", "It is raining: the rain waters the beds.");
            case TaskEffect.Water when !ThirstyBeds(village, now, owner).Any():
                throw new GameRuleException("farm.nothingToWater", "No bed needs water now.");
            case TaskEffect.Harvest when RipeBed(village, now, owner) is null:
                throw new GameRuleException("farm.nothingRipe", "Nothing is ripe yet.");
        }
    }

    /// <summary>Sows the crop in the first empty bed. Returns false when someone sowed the last one first.</summary>
    public static bool Sow(Village village, CropKind crop, DateTimeOffset now, Guid? owner = null)
    {
        EnsureBeds(village);
        if (EmptyBed(village, owner) is not { } bed)
        {
            return false;
        }

        bed.Crop = crop;
        bed.Growth = 0;
        bed.UpdatedAt = now;
        return true;
    }

    /// <summary>Waters the thirstiest beds. Returns how many were watered.</summary>
    public static int Water(Village village, DateTimeOffset now, Guid? owner = null)
    {
        EnsureBeds(village);
        var watered = ThirstyBeds(village, now, owner).Take(FarmCatalog.BedsPerWatering).ToList();
        foreach (var bed in watered)
        {
            bed.Growth = GrowthOf(village, bed, now);
            bed.UpdatedAt = now;
            bed.WateredUntil = now + FarmCatalog.WaterDuration;
        }

        return watered.Count;
    }

    /// <summary>Empties the first ripe bed and returns its crop; null when someone harvested it first.</summary>
    public static CropDefinition? Harvest(Village village, DateTimeOffset now, Guid? owner = null)
    {
        EnsureBeds(village);
        if (RipeBed(village, now, owner) is not { Crop: { } crop } bed)
        {
            return null;
        }

        bed.Crop = null;
        bed.Growth = 0;
        bed.UpdatedAt = now;
        return FarmCatalog.Get(crop);
    }

    /// <summary>
    /// Walking time of farm work: water is carried from the river to the beds, the harvest from the beds to storage.
    /// Null for other tasks.
    /// </summary>
    public static TimeSpan? Walk(Village village, TaskDefinition task, DateTimeOffset now, Guid? owner = null)
    {
        switch (task.Effect)
        {
            case TaskEffect.Sow:
                return TimeSpan.Zero;
            case TaskEffect.Water:
            {
                var bed = ThirstyBeds(village, now, owner).FirstOrDefault();
                return bed is null ? TimeSpan.Zero : WorldMap.Walk(WorldMap.Places[WorldMap.Ford], WorldMap.Yard(bed.PlotId));
            }

            case TaskEffect.Harvest:
            {
                if (RipeBed(village, now, owner) is not { Crop: { } crop } bed)
                {
                    return TimeSpan.Zero;
                }

                var yard = WorldMap.Yard(bed.PlotId);
                return WorldMap.Walk(yard, village.DropPoint(FarmCatalog.Get(crop).Harvest[0].Resource, yard));
            }

            default:
                return null;
        }
    }

    private static IEnumerable<FieldBed> Beds(Village village, Guid? owner = null) => village.Fields.Where(b=>village.Structures.Any(s=>s.PlotId==b.PlotId && s.Kind==BuildingKind.Farm && s.OwnerAccountId==owner && !s.Decommissioning)).OrderBy(b => b.PlotId).ThenBy(b => b.Index);
}
