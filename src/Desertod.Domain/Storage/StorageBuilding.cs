using Desertod.Domain.Catalog;

namespace Desertod.Domain.Storage;

/// <summary>
/// A building that holds goods in slots. A slot holds one kind of good, so a building with free units
/// can still refuse a new kind when all its slots are taken.
/// </summary>
public sealed class StorageBuilding
{
    public StorageKind Kind { get; set; }

    /// <summary>Unique within a village; identifies the building.</summary>
    public required string PlotId { get; set; }

    public DateTimeOffset BuiltAt { get; set; }

    /// <summary>Harm done by raiders, 0 to <see cref="Combat.CombatRules.MaximumDamage"/>; repaired by citizens.</summary>
    public int Damage { get; set; }

    public List<StoredGood> Goods { get; set; } = [];

    public StorageDefinition Definition => StorageCatalog.Get(Kind);

    public static StorageBuilding Create(StorageKind kind, string plotId, DateTimeOffset now) =>
        new() { Kind = kind, PlotId = plotId, BuiltAt = now };

    public int Amount(ResourceKind kind) => Goods.SingleOrDefault(g => g.Kind == kind)?.Amount ?? 0;

    public int SlotsUsed(int amount) => (amount + Definition.SlotSize - 1) / Definition.SlotSize;

    public int FreeSpace(ResourceKind kind)
    {
        if (!Definition.AcceptsGood(kind))
        {
            return 0;
        }

        var slotsForOthers = Goods.Where(g => g.Kind != kind).Sum(g => SlotsUsed(g.Amount));
        return Math.Max(0, (Definition.Slots - slotsForOthers) * Definition.SlotSize - Amount(kind));
    }

    /// <summary>Stores as much as fits and returns the stored amount.</summary>
    public int Put(ResourceKind kind, int amount)
    {
        var stored = Math.Min(amount, FreeSpace(kind));
        if (stored <= 0)
        {
            return 0;
        }

        var good = Goods.SingleOrDefault(g => g.Kind == kind);
        if (good is null)
        {
            good = new StoredGood { Kind = kind };
            Goods.Add(good);
        }

        good.Amount += stored;
        return stored;
    }

    /// <summary>Takes up to <paramref name="amount"/> and returns the amount taken.</summary>
    public int Take(ResourceKind kind, int amount)
    {
        var good = Goods.SingleOrDefault(g => g.Kind == kind);
        if (good is null)
        {
            return 0;
        }

        var taken = Math.Min(amount, good.Amount);
        good.Amount -= taken;
        if (good.Amount <= 0)
        {
            Goods.Remove(good);
        }

        return taken;
    }

    /// <summary>Fill of every occupied slot, 0 to 1, for drawing stacks on the map.</summary>
    public IEnumerable<(ResourceKind Kind, double Fill)> Slots()
    {
        var size = Definition.SlotSize;
        foreach (var good in Goods.OrderBy(g => g.Kind))
        {
            var remaining = good.Amount;
            while (remaining > 0)
            {
                var fill = Math.Min(size, remaining);
                yield return (good.Kind, (double)fill / size);
                remaining -= fill;
            }
        }
    }
}

public sealed class StoredGood
{
    public ResourceKind Kind { get; set; }
    public int Amount { get; set; }
}
