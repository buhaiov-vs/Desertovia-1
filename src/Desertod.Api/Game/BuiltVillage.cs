using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;

namespace Desertod.Api.Game;

/// <summary>
/// Development only: a village with every building, storage and project finished, every technology discovered and the
/// whole land explored, so their pictures can be checked side by side. It is rebuilt on every start; its Tsar is a development login.
/// </summary>
public static class BuiltVillage
{
    public const string Slug = "demo-built";
    private const string Name = "Демо: Отстроенная деревня";
    private const string TsarName = "Демо-зодчий";
    private const int StockPerGood = 40;
    private const int Knowledge = 200;

    /// <summary>Natural places left free: the clearing around the campfire, the trail to the gate, berries and stones.</summary>
    private static readonly TileArea[] KeptClear =
    [
        new(12, 10, 7, 7),
        new(0, 12, 12, 3),
        new(1, 16, 7, 5),
        new(8, 1, 5, 4),
    ];

    private static readonly (CropKind Crop, double Growth)[] Beds = [(CropKind.Rye, 0.2), (CropKind.Cabbage, 0.6), (CropKind.Flax, 1)];

    public static async Task SeedAsync(DesertodDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        await DemoVillage.RemoveAsync(db, Slug, cancellationToken);
        var now = time.GetUtcNow();

        var tsar = await DemoVillage.AccountAsync(db, TsarName, now, cancellationToken);
        var village = Village.Create(Slug, Name, "Все здания, склады и стройки готовы: посмотреть, как они выглядят.", false, tsar.Id, now);

        using var plots = FreePlots().GetEnumerator();
        string NextPlot() => plots.MoveNext() ? plots.Current : throw new InvalidOperationException("The camp has no free plot left.");

        foreach (var storage in StorageCatalog.Storages.Where(s => s.Kind != StorageKind.GreatGranary))
        {
            village.Storages.Add(StorageBuilding.Create(storage.Kind, NextPlot(), now));
        }

        foreach (var building in BuildingCatalog.Buildings.Where(b => b.Storage is null && b.Kind != BuildingKind.RopeBridge))
        {
            village.Structures.Add(new Structure { Kind = building.Kind, PlotId = NextPlot(), BuiltAt = now });
        }

        if (BridgePlot(village) is { } bridge)
        {
            village.Structures.Add(new Structure { Kind = BuildingKind.RopeBridge, PlotId = bridge, BuiltAt = now });
        }

        village.Explored = Enumerable.Repeat(byte.MaxValue, Exploration.Size).ToArray();

        FarmRules.EnsureBeds(village);
        foreach (var (bed, (crop, growth)) in village.Fields.OrderBy(b => b.Index).Zip(Beds))
        {
            bed.Crop = crop;
            bed.Growth = growth;
            bed.UpdatedAt = now;
        }

        foreach (var technology in TechnologyCatalog.Technologies)
        {
            village.Research.Add(new TechnologyProgress { TechnologyId = technology.Id, Done = technology.Cycles, ResearchedAt = now });
        }

        foreach (var project in ProjectCatalog.Projects.Where(p => p.Reward.Storage is not null))
        {
            village.Storages.Add(StorageBuilding.Create(project.Reward.Storage!.Value, project.Id, now));
        }

        foreach (var kind in Enum.GetValues<ResourceKind>().Where(StorageCatalog.IsGood))
        {
            village.Store(kind, StockPerGood);
        }

        village.Store(ResourceKind.ResearchPoints, Knowledge);
        db.Villages.Add(village);

        foreach (var kind in StoryQuests.Chain)
        {
            var quest = VillageQuest.Open(village.Id, kind, null, StoryQuests.Target(kind), now);
            quest.Status = QuestStatus.Completed;
            quest.CompletedAt = now;
            db.VillageQuests.Add(quest);
        }

        foreach (var definition in ProjectCatalog.Projects)
        {
            var finished = VillageProject.Start(village.Id, definition, now);
            foreach (var material in definition.Materials)
            {
                finished.Materials.Single(m => m.Kind == material.Resource).Delivered = material.Amount;
            }

            finished.SurveyProgress = definition.SurveyCycles;
            finished.BuildProgress = definition.BuildCycles;
            finished.Status = ProjectStatus.Completed;
            finished.CompletedAt = now;
            db.VillageProjects.Add(finished);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Plots of the camp that keep clear of reserved ground and natural places, nearest the campfire first.</summary>
    private static IEnumerable<string> FreePlots()
    {
        var area = WorldMap.BuildArea;
        var camp = WorldMap.Places[WorldMap.Camp];
        var plots = new List<string>();
        for (var y = area.Y; y + WorldMap.PlotHeight <= area.Y + area.Height; y += WorldMap.PlotHeight)
        {
            for (var x = area.X; x + WorldMap.PlotWidth <= area.X + area.Width; x += WorldMap.PlotWidth)
            {
                var footprint = WorldMap.Footprint(x, y);
                if (!WorldMap.ReservedAreas.Concat(KeptClear).Any(taken => taken.Overlaps(footprint)))
                {
                    plots.Add(WorldMap.PlotAt(x, y));
                }
            }
        }

        return plots.OrderBy(plotId => WorldMap.Yard(plotId).DistanceTo(camp));
    }

    /// <summary>The spot nearest the campfire where a rope bridge spans the ravine, or null when there is none.</summary>
    private static string? BridgePlot(Village village)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        return (village.Wilds?.Ravine ?? [])
            .SelectMany(span => Enumerable.Range(span.Top + span.Height - WorldMap.PlotHeight + 1, WorldMap.PlotHeight)
                .Select(y => (X: span.X, Y: y)))
            .Where(plot => Wilderness.Spans(village, WorldMap.Footprint(plot.X, plot.Y)))
            .Select(plot => WorldMap.PlotAt(plot.X, plot.Y))
            .OrderBy(plotId => WorldMap.Yard(plotId).DistanceTo(camp))
            .FirstOrDefault();
    }
}
