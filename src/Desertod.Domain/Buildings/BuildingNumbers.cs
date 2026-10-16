using Desertod.Domain.Entities;
namespace Desertod.Domain.Buildings;

public static class BuildingNumbers
{
    /// <summary>Allocate village-wide monotonic numbers, retaining the identity throughout upgrades.</summary>
    public static void Ensure(Village village)
    {
        var allNumbers = village.Structures.Select(s=>s.Number).Concat(village.Storages.Select(s=>s.Number)).Concat(village.Sites.Select(s=>s.Number)).ToArray();
        if (allNumbers.All(n=>n>0) && village.BuildingSequence >= allNumbers.DefaultIfEmpty().Max()) return;
        var plots = village.Structures.Select(s => (s.PlotId, s.BuiltAt, s.Number))
            .Concat(village.Storages.Select(s => (s.PlotId, s.BuiltAt, s.Number)))
            .Concat(village.Sites.Select(s => (s.PlotId, BuiltAt: s.PlacedAt, s.Number)))
            .GroupBy(s => s.PlotId).OrderBy(g => g.Min(s => s.BuiltAt)).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
        village.BuildingSequence = Math.Max(village.BuildingSequence, plots.SelectMany(g => g).Select(s => s.Number).DefaultIfEmpty().Max());
        foreach (var group in plots)
        {
            var number = group.Where(s => s.Number > 0).Select(s => s.Number).DefaultIfEmpty().Min();
            if (number == 0) number = checked(++village.BuildingSequence);
            foreach (var item in village.Structures.Where(s => s.PlotId == group.Key)) item.Number = number;
            foreach (var item in village.Storages.Where(s => s.PlotId == group.Key)) item.Number = number;
            foreach (var item in village.Sites.Where(s => s.PlotId == group.Key)) item.Number = number;
        }
    }
}
