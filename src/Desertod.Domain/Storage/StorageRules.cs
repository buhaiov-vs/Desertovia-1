using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;

namespace Desertod.Domain.Storage;

public static class StorageRules
{
    /// <summary>Most of one good a citizen may hold, whether taken out of storage or left over from a task.</summary>
    public const int TakeLimit = 10;

    /// <summary>Puts as much of the citizen's load into storage as fits. Returns what was unloaded.</summary>
    public static IReadOnlyList<ResourceAmount> Unload(Citizen citizen, Village village)
    {
        var unloaded = new List<ResourceAmount>();
        foreach (var good in citizen.Load.ToList())
        {
            var stored = village.Store(good.Kind, good.Amount);
            if (stored > 0)
            {
                citizen.TakeFromLoad(good.Kind, stored);
                unloaded.Add(new ResourceAmount(good.Kind, stored));
            }
        }

        return unloaded;
    }

    /// <summary>
    /// An idle citizen takes up to <paramref name="amount"/> of a good out of one storage building into their hands,
    /// holding at most <see cref="TakeLimit"/>. Returns the amount taken.
    /// </summary>
    public static int Take(Citizen citizen, Village village, string plotId, ResourceKind kind, int amount, DateTimeOffset now)
    {
        if (amount < 1)
        {
            throw new GameRuleException("take.amount", "Take at least one.");
        }

        if (citizen.Status != CitizenStatus.Active)
        {
            throw new GameRuleException("citizen.dormant", "Dormant citizens cannot work.");
        }

        if (citizen.WorkState(now) == WorkState.Working)
        {
            throw new GameRuleException("take.working", "Finish the current task first.");
        }

        var building = village.Storages.SingleOrDefault(s => s.PlotId == plotId)
                       ?? throw new GameRuleException("take.noStorage", "There is no storage building here.");

        var room = TakeLimit - citizen.Carried(kind);
        if (room <= 0)
        {
            throw new GameRuleException("take.handsFull", $"You cannot carry more {ResourceNames.Of(kind)}.", Resource(kind));
        }

        var taken = building.Take(kind, Math.Min(amount, room));
        if (taken <= 0)
        {
            throw new GameRuleException("take.nothing", $"There is no {ResourceNames.Of(kind)} here.", Resource(kind));
        }

        citizen.Carry(kind, taken);
        return taken;
    }

    /// <summary>
    /// Throws away only the goods storage has no room for; the rest stays in hands to be carried back,
    /// so goods taken out of storage cannot be destroyed.
    /// </summary>
    public static void Drop(Citizen citizen, Village village)
    {
        if (citizen.Load.Count == 0)
        {
            throw new GameRuleException("hands.empty", "Your hands are empty.");
        }

        var unwanted = citizen.Load.Where(g => village.FreeSpace(g.Kind) <= 0).ToList();
        if (unwanted.Count == 0)
        {
            throw new GameRuleException("drop.roomInStorage", "Storage has room for what you carry. Carry it there instead.");
        }

        foreach (var good in unwanted)
        {
            citizen.TakeFromLoad(good.Kind, good.Amount);
        }
    }

    private static Dictionary<string, object> Resource(ResourceKind kind) => new() { ["resource"] = kind.ToString() };
}
