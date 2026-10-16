using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

/// <summary>A persistent part of the existing palisade, shared by every raid and normal repair work.</summary>
public sealed class Fortification
{
    public required string Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public bool Gate { get; set; }
    public bool Open { get; set; }
    public int Damage { get; set; }
    public bool Blocks => Damage < CombatRules.MaximumDamage && (!Gate || !Open);
}

public static class Fortifications
{
    // Matches the author's PALISADE in web/src/game/worldMap.ts. Expansion does not silently move a built wall.
    public static void Ensure(Village village, IReadOnlyCollection<string> completed)
    {
        if (!completed.Contains(ProjectCatalog.Palisade)) return;
        void Add(int x, int y, bool gate = false) => village.Fortifications.Add(new()
            { Id = $"wall:{x},{y}", X = x, Y = y, Gate = gate, Open = gate });
        if (village.Fortifications.Count == 0)
        {
            for (var y = -1; y <= 26; y++) Add(-1, y, y is >= 12 and <= 14);
            for (var x = 0; x <= 27; x++) { Add(x, -1); Add(x, 26); }
        }
        // Close only the old missing river end caps; never repair or recreate existing pieces.
        var seed = Wilderness.Seed(village.Id.ToString());
        var terrain = AuthoredTerrain.For(village.Id, village.Wilds);
        foreach (var y in new[] { -1, 26 })
        {
            if (At(village, 27, y) is null) continue;
            for (var x = 28; x < Wilderness.RiverWestBank(y, seed); x++)
            {
                if (!AuthoredTerrain.Inside(x,y) || terrain[AuthoredTerrain.Index(x,y)] == AuthoredTerrain.Water) break;
                if (At(village, x,y) is null) Add(x,y);
            }
        }
    }

    public static Fortification? At(Village village, int x, int y) => village.Fortifications.FirstOrDefault(f => f.X == x && f.Y == y);
    public static TilePoint Point(Fortification f) => new(f.X + .5, f.Y + .5);
}
