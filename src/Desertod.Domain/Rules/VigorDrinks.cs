using Desertod.Domain.Economy;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;

namespace Desertod.Domain.Rules;

/// <summary>Brewed at the tavern once the village discovers vigor drinks; drinking one gives energy back.</summary>
public static class VigorDrinks
{
    public static DateTimeOffset? ReadyAt(Citizen citizen) => citizen.LastDrinkAt + GameRules.VigorDrinkCooldown;

    /// <summary>Takes one owned drink first, then from the citizen's hands or the village storage.</summary>
    public static void Drink(Citizen citizen, Village village, DateTimeOffset now)
    {
        if (citizen.Status == CitizenStatus.Dormant)
        {
            throw new GameRuleException("citizen.dormant", "Dormant citizens cannot work.");
        }

        if(citizen.ExhaustedUntil > now) throw new GameRuleException("citizen.exhausted","You cannot drink while unconscious.");
        if (ReadyAt(citizen) is { } readyAt && now < readyAt)
        {
            throw new GameRuleException("drink.cooldown", "Your heart is still racing from the last drink.",
                new Dictionary<string, object> { ["readyAt"] = readyAt });
        }

        if (citizen.CurrentEnergy(now, village) >= citizen.EnergyLimit(now))
        {
            throw new GameRuleException("drink.full", "You are full of energy already.");
        }

        if (PersonalInventory.Amount(citizen, ResourceKind.VigorDrink) > 0)
            PersonalInventory.Remove(citizen, ResourceKind.VigorDrink, 1);
        else if (citizen.TakeFromLoad(ResourceKind.VigorDrink, 1) == 0 && village.Take(ResourceKind.VigorDrink, 1) == 0)
        {
            throw new GameRuleException("drink.none", "There is no vigor drink in your hands or in storage.");
        }

        citizen.FlushEnergy(now, village);
        citizen.StoredEnergy = Math.Min(citizen.EnergyLimit(now), citizen.StoredEnergy + GameRules.VigorDrinkEnergy);
        citizen.LastDrinkAt = now;
    }
}
