using Desertod.Domain.Economy;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;

namespace Desertod.Domain.Rules;

/// <summary>Brewed at the tavern once the village discovers vigor drinks; drinking one gives energy back.</summary>
public static class VigorDrinks
{
    public static DateTimeOffset? ReadyAt(Citizen citizen, Village? village = null) =>
        citizen.LastDrinkAt + TimeSpan.FromMinutes(village?.VigorCooldownMinutes ?? 240);

    public static DateTimeOffset? EnergyDrinkReadyAt(Citizen citizen) => citizen.LastEnergyDrinkAt?.AddHours(12);

    public static void RestoreFully(Citizen citizen, Village village, DateTimeOffset now)
    {
        HospitalRules.RequireReady(citizen, now);
        if (citizen.Status == CitizenStatus.Dormant || citizen.ExhaustedUntil > now)
            throw new GameRuleException("citizen.exhausted", "You cannot drink while unconscious.");
        if (EnergyDrinkReadyAt(citizen) > now)
            throw new GameRuleException("drink.cooldown", "Your heart is still racing from the last drink.");
        if (citizen.CurrentEnergy(now, village) >= citizen.EnergyLimit(now))
            throw new GameRuleException("drink.full", "You are full of energy already.");
        citizen.FlushEnergy(now, village);
        citizen.StoredEnergy = citizen.EnergyLimit(now);
        citizen.LastEnergyDrinkAt = now;
    }

    /// <summary>Takes one owned drink first, then from the citizen's hands or the village storage.</summary>
    public static void Drink(Citizen citizen, Village village, DateTimeOffset now)
    {
        if (citizen.Status == CitizenStatus.Dormant)
        {
            throw new GameRuleException("citizen.dormant", "Dormant citizens cannot work.");
        }

        if(citizen.ExhaustedUntil > now) throw new GameRuleException("citizen.exhausted","You cannot drink while unconscious.");
        if (ReadyAt(citizen, village) is { } readyAt && now < readyAt)
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
        citizen.StoredEnergy = Math.Min(citizen.EnergyLimit(now), citizen.StoredEnergy + village.VigorEnergy);
        citizen.LastDrinkAt = now;
    }
}


public static class VigorTuning
{
    public static IReadOnlyList<Desertod.Domain.Director.AlmightyOption> Options(Village village)
    {
        var result = new List<Desertod.Domain.Director.AlmightyOption>();
        void Add(string id, string title, int energy, int cooldown) => result.Add(new(id, title,
            $"Взвар: +{village.VigorEnergy} → +{energy} сил; перерыв {village.VigorCooldownMinutes} → {cooldown} реальных минут. Рецепт и время работы сохраняются. Только после согласия царя."));
        if (village.VigorEnergy < 16) Add("vigor_stronger", "Усилить бодрящий взвар", village.VigorEnergy + 2, village.VigorCooldownMinutes);
        if (village.VigorEnergy > 4) Add("vigor_weaker", "Ослабить бодрящий взвар", village.VigorEnergy - 2, village.VigorCooldownMinutes);
        if (village.VigorCooldownMinutes > 120) Add("vigor_frequent", "Сократить перерыв взвара", village.VigorEnergy, village.VigorCooldownMinutes - 60);
        if (village.VigorCooldownMinutes < 480) Add("vigor_rare", "Увеличить перерыв взвара", village.VigorEnergy, village.VigorCooldownMinutes + 60);
        return result;
    }

    public static void Apply(Village village, string option)
    {
        if (!Options(village).Any(o => o.Id == option)) throw new GameRuleException("director.proposal", "Настройка взвара недоступна.");
        switch (option)
        {
            case "vigor_stronger": village.VigorEnergy += 2; break;
            case "vigor_weaker": village.VigorEnergy -= 2; break;
            case "vigor_frequent": village.VigorCooldownMinutes -= 60; break;
            case "vigor_rare": village.VigorCooldownMinutes += 60; break;
        }
    }
}
