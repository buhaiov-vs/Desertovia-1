using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Domain.Rules;

/// <summary>
/// Gold veins. One stands just outside a den on each of levels 5, 10, 15, 20 and 25.
/// Each vein holds <see cref="Size"/> gold and does not grow back. The old patch by the river is not a vein.
/// </summary>
public static class GoldVein
{
    public const string DigTask = "dig-gold";
    public const int Size = 150;

    /// <summary>Foe rings that carry a gold vein. The vein stands just outside one den of that level.</summary>
    public static readonly int[] Levels = [5, 10, 15, 20, 25];

    public static bool IsDug(TaskDefinition task) => task.Workplace == WorldMap.GoldVein;

    public static int Remaining(Village village)
    {
        var gold = village.Wilds?.Gold;
        if (gold is null || gold.Count == 0)
        {
            return Math.Max(0, Size - village.GoldVeinMined);
        }

        return gold.Sum(vein => vein.Remaining);
    }

    public static bool IsEmpty(Village village) => Remaining(village) == 0;

    public static GoldDeposit? Find(Village village, TilePoint spot) =>
        village.Wilds?.Gold.Where(vein => vein.Center.DistanceTo(spot) <= Wilderness.SpotReach)
            .MinBy(vein => vein.Center.DistanceTo(spot));

    /// <summary>The vein a dig works: the one under <paramref name="spot"/>, else the nearest that still has gold.</summary>
    public static GoldDeposit? Working(Village village, TilePoint spot) =>
        Find(village, spot)
        ?? village.Wilds?.Gold.Where(vein => vein.Remaining > 0)
            .MinBy(vein => vein.Center.DistanceTo(WorldMap.Places[WorldMap.Camp]));

    /// <summary>Takes up to <paramref name="amount"/> gold from the vein at <paramref name="spot"/> and returns what was taken.</summary>
    public static int Take(Village village, int amount, TilePoint spot)
    {
        var vein = Working(village, spot);
        var taken = vein is null
            ? Math.Min(Math.Max(0, amount), Math.Max(0, Size - village.GoldVeinMined))
            : Math.Min(Math.Max(0, amount), vein.Remaining);
        if (vein is not null)
        {
            vein.Mined += taken;
        }

        village.GoldVeinMined += taken;
        return taken;
    }

    /// <summary>
    /// Puts a vein beside one den of each <see cref="Levels"/> ring. Dug gold on a tile that stays put is kept.
    /// Gold dug before these veins existed is counted against the nearest one.
    /// </summary>
    public static bool Sync(Wilds wilds, uint seed, int alreadyMined)
    {
        wilds.Gold ??= [];
        var placed = Place(seed);
        var same = wilds.Gold.Count == placed.Count
            && wilds.Gold.Select(vein => (vein.X, vein.Y, vein.Level)).SequenceEqual(placed.Select(vein => (vein.X, vein.Y, vein.Level)));
        if (same)
        {
            return false;
        }

        var dug = wilds.Gold.ToDictionary(vein => (vein.X, vein.Y), vein => vein.Mined);
        var hadNone = wilds.Gold.Count == 0;
        wilds.Gold = placed;
        foreach (var vein in wilds.Gold)
        {
            if (dug.TryGetValue((vein.X, vein.Y), out var mined))
            {
                vein.Mined = mined;
            }
        }

        if (hadNone && alreadyMined > 0 && wilds.Gold.Count > 0)
        {
            var nearest = wilds.Gold.MinBy(vein => vein.Center.DistanceTo(WorldMap.Places[WorldMap.Camp]))!;
            nearest.Mined = Math.Min(Size, alreadyMined);
        }

        return true;
    }

    /// <summary>One vein per chosen foe level, just outside that den, on the side away from the camp when that tile is open.</summary>
    public static List<GoldDeposit> Place(uint seed)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        var dens = SpawnRings.Points(seed);
        var stone = CaveSite.At(seed);
        var coal = CaveSite.At(seed, Wilderness.CoalSalt, [stone]);
        var iron = CaveSite.At(seed, Wilderness.IronSalt, [stone, coal]);
        var moon = CaveSite.At(seed, Wilderness.MoonstoneSalt, [stone, coal, iron]);
        var taken = new List<TilePoint> { stone.Mouth, coal.Mouth, iron.Mouth, moon.Mouth };

        var veins = new List<GoldDeposit>();
        foreach (var level in Levels)
        {
            var den = dens.FirstOrDefault(point => point.Level == level);
            if (den is null)
            {
                continue;
            }

            var away = Math.Atan2(den.Center.Y - camp.Y, den.Center.X - camp.X);
            for (var turn = 0; turn < 8; turn++)
            {
                var angle = away + turn * Math.PI / 4;
                var reach = den.Claim / 2.0 + 8;
                var x = (int)Math.Floor(den.Center.X + Math.Cos(angle) * reach);
                var y = (int)Math.Floor(den.Center.Y + Math.Sin(angle) * reach);
                var center = new TilePoint(x + 0.5, y + 0.5);
                var tile = new TileArea(x, y, 1, 1);
                var ground = FarWilds.At(x, y, seed);
                if (!WorldMap.Land.Contains(tile) || WorldMap.Vale.Contains(tile) || FarWilds.Blocks(ground) || ground == FarWilds.Kind.Water)
                {
                    continue;
                }

                if (center.DistanceTo(den.Center) <= den.Claim / 2.0)
                {
                    continue;
                }

                if (taken.Any(other => other.DistanceTo(center) < 30))
                {
                    continue;
                }

                veins.Add(new GoldDeposit { X = x, Y = y, Level = level });
                taken.Add(center);
                break;
            }
        }

        return veins;
    }
}

