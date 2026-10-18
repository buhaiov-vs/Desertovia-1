using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Storage;

namespace Desertod.Domain.Combat;

/// <summary>
/// A smith grinds one sword, spear or arrow a single step sharper. The edge is kept on the stack and shown as +N.
/// Each step adds <see cref="PowerPerLevel"/> to the fight power, up to <see cref="Maximum"/>. A bow is not sharpened:
/// the arrows are.
/// </summary>
public static class WeaponSharpening
{
    public const int Maximum = 3;
    public const int PowerPerLevel = 1;

    public static int Bonus(int level) => PowerPerLevel * Math.Clamp(level, 0, Maximum);

    public static bool IsSharpenable(ResourceKind kind) =>
        kind is ResourceKind.Sword or ResourceKind.Spear or ResourceKind.Arrows;

    /// <summary>Throws when this citizen cannot grind one of these pieces right now.</summary>
    public static void Check(Citizen citizen, Village village, ResourceKind kind)
    {
        if (!IsSharpenable(kind))
        {
            throw new GameRuleException("sharpen.weapon", "A sword, a spear or an arrow can be sharpened.");
        }

        if (kind != ResourceKind.Arrows && citizen.Weapon is { } held && held != kind)
        {
            throw new GameRuleException("sharpen.holding", "Put the other weapon back into storage before sharpening.");
        }

        if (kind != ResourceKind.Arrows && citizen.Weapon == kind)
        {
            if (citizen.WeaponSharpen >= Maximum)
            {
                throw new GameRuleException("sharpen.maximum", "This weapon is already as sharp as the smith can make it.");
            }

            return;
        }

        var levels = Levels(citizen, village, kind).ToList();
        if (levels.Any(level => level < Maximum))
        {
            return;
        }

        if (levels.Count > 0)
        {
            throw new GameRuleException("sharpen.maximum", "This weapon is already as sharp as the smith can make it.");
        }

        throw new GameRuleException("storage.missingInput", $"Not enough {ResourceNames.Of(kind)} in storage.",
            new Dictionary<string, object> { ["resource"] = kind.ToString(), ["missing"] = 1 });
    }

    /// <summary>Puts the dullest piece of this kind into the citizen's hand. Check first.</summary>
    public static void Take(Citizen citizen, Village village, ResourceKind kind)
    {
        if (kind == ResourceKind.Arrows)
        {
            var arrow = Levels(citizen, village, kind).Where(level => level < Maximum).Min();
            if (citizen.TakeFromLoad(kind, 1, arrow) == 0 && !TakeFromQuiver(citizen, arrow))
            {
                village.Take(kind, 1, arrow);
            }

            citizen.Carry(kind, 1, arrow);
            return;
        }

        if (citizen.Weapon == kind)
        {
            return;
        }

        var level = Levels(citizen, village, kind).Where(level => level < Maximum).Min();
        var fromHands = citizen.TakeFromLoad(kind, 1, level);
        if (fromHands == 0)
        {
            village.Take(kind, 1, level);
        }

        citizen.Weapon = kind;
        citizen.WeaponSharpen = level;
    }

    /// <summary>Raises one carried arrow one step and puts it back. Returns the new edge.</summary>
    public static int? Raise(Citizen citizen, Village village)
    {
        if (DullestCarriedArrow(citizen) is not int level)
        {
            return null;
        }

        citizen.TakeFromLoad(ResourceKind.Arrows, 1, level);
        var next = level + 1;
        if (village.Store(ResourceKind.Arrows, 1, next) == 0)
        {
            citizen.Carry(ResourceKind.Arrows, 1, next);
        }

        return next;
    }

    /// <summary>Puts the arrow taken for sharpening back into storage, still at its old edge.</summary>
    public static void Return(Citizen citizen, Village village)
    {
        if (DullestCarriedArrow(citizen) is not int level)
        {
            return;
        }

        citizen.TakeFromLoad(ResourceKind.Arrows, 1, level);
        if (village.Store(ResourceKind.Arrows, 1, level) == 0)
        {
            citizen.Carry(ResourceKind.Arrows, 1, level);
        }
    }

    /// <summary>Sharpest arrow stack with at least <paramref name="needed"/> arrows.</summary>
    public static int? BestEdge(Citizen citizen, Village village, int needed)
    {
        if (needed < 1)
        {
            needed = 1;
        }

        for (var level = Maximum; level >= 0; level--)
        {
            if (Count(citizen, village, level) >= needed)
            {
                return level;
            }
        }

        return null;
    }

    /// <summary>Arrows of one edge in the quiver, in the hands, or in village storage.</summary>
    public static int Count(Citizen citizen, Village village, int sharpen)
    {
        var count = citizen.Load.Where(good => good.Kind == ResourceKind.Arrows && good.Sharpen == sharpen).Sum(good => good.Amount);
        if (citizen.Arrows > 0 && citizen.ArrowSharpen == sharpen)
        {
            count += citizen.Arrows;
        }

        foreach (var storage in OpenStorages(village))
        {
            count += storage.Goods.Where(good => good.Kind == ResourceKind.Arrows && good.Sharpen == sharpen).Sum(good => good.Amount);
        }

        return count;
    }

    /// <summary>Sharpen levels of this piece in the citizen's hands, quiver or village storage.</summary>
    public static IEnumerable<int> Levels(Citizen citizen, Village village, ResourceKind kind)
    {
        foreach (var good in citizen.Load)
        {
            if (good.Kind == kind && good.Amount > 0)
            {
                yield return good.Sharpen;
            }
        }

        if (kind == ResourceKind.Arrows && citizen.Arrows > 0)
        {
            yield return citizen.ArrowSharpen;
        }

        foreach (var storage in OpenStorages(village))
        {
            foreach (var good in storage.Goods)
            {
                if (good.Kind == kind && good.Amount > 0)
                {
                    yield return good.Sharpen;
                }
            }
        }
    }

    private static int? DullestCarriedArrow(Citizen citizen)
    {
        var levels = citizen.Load
            .Where(good => good.Kind == ResourceKind.Arrows && good.Amount > 0 && good.Sharpen < Maximum)
            .Select(good => good.Sharpen)
            .ToList();
        return levels.Count == 0 ? null : levels.Min();
    }

    private static bool TakeFromQuiver(Citizen citizen, int level)
    {
        if (citizen.Arrows <= 0 || citizen.ArrowSharpen != level)
        {
            return false;
        }

        citizen.SpendArrows(1);
        return true;
    }

    private static IEnumerable<StorageBuilding> OpenStorages(Village village) =>
        village.Storages.Where(storage => storage.OwnerAccountId is null && storage.RuinedAt is null && storage.Damage < 100);
}
