using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;

namespace Desertod.Domain.Rules;

public static class FoodRules
{
    /// <summary>One meal, hands first, own inventory next, public stock only when allowed. Never restores energy.</summary>
    public static bool Eat(Citizen citizen, Village village, DateTimeOffset now, bool allowPublic)
    {
        var eaten = citizen.TakeFromLoad(ResourceKind.Food, 1);
        if (eaten == 0 && PersonalInventory.Amount(citizen, ResourceKind.Food) > 0)
        {
            PersonalInventory.Remove(citizen, ResourceKind.Food, 1);
            eaten = 1;
        }
        if (eaten == 0 && allowPublic) eaten = village.Take(ResourceKind.Food, 1);
        var hungry = eaten == 0;
        if (citizen.IsHungry != hungry)
        {
            citizen.FlushEnergy(now, village);
            citizen.IsHungry = hungry;
        }
        if (!hungry) citizen.CyclesSinceMeal = 0;
        return !hungry;
    }
}
