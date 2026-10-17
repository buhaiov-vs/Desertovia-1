using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;

namespace Desertod.Domain.Rules;

public static class PreparedMeals
{
    public static readonly TimeSpan Duration=TimeSpan.FromMinutes(20);
    public static readonly TimeSpan Cooldown=TimeSpan.FromMinutes(30);
    public static double WorkMultiplier(Citizen c,DateTimeOffset now)=>c.MealKind==ResourceKind.Porridge&&c.MealUntil>now?.9:1;
    public static double AttackMultiplier(Citizen c,DateTimeOffset now)=>c.MealKind==ResourceKind.Stew&&c.MealUntil>now?1.05:1;
    public static void Eat(Citizen c,Village v,ResourceKind kind,DateTimeOffset now)
    {
        if(kind is not (ResourceKind.Porridge or ResourceKind.Stew))throw new GameRuleException("meal.unknown","Choose a prepared meal.");
        HospitalRules.RequireReady(c,now);
        if(CombatRules.IsInjured(c,now)||c.Status==CitizenStatus.Dormant||c.ExhaustedUntil>now||c.DungeonRunId is not null)
            throw new GameRuleException("meal.unavailable","Wait until recovery or the expedition is finished.");
        if(c.MealUntil>now||c.LastMealAt+Cooldown>now)throw new GameRuleException("meal.cooldown","Wait before eating another prepared meal.");
        if(c.Carried(kind)>0)c.TakeFromLoad(kind,1);
        else if(PersonalInventory.Amount(c,kind)>0)PersonalInventory.Remove(c,kind,1);
        else throw new GameRuleException("meal.none","Take a portion into your backpack first.");
        c.MealKind=kind;c.MealUntil=now+Duration;c.LastMealAt=now;
        // Nutrition only: flush the old hunger-dependent recovery rate; grant no HP or energy.
        if(c.IsHungry)c.FlushEnergy(now,v);
        c.IsHungry=false;c.CyclesSinceMeal=0;
    }
}
