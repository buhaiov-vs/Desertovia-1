using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;

namespace Desertod.Domain.Combat;

/// <summary>Small, finite stat training from HP actually removed, settled once with battle rewards.</summary>
public static class CombatTraining
{
    public const int MaximumExperience = 10;
    public static void Grant(Citizen citizen,RaidFighter fighter)
    {
        if(fighter.DamageDealt<=0||!double.IsFinite(fighter.DamageDealt))return;
        var total=Math.Min(MaximumExperience,Math.Max(1,(int)Math.Ceiling(Math.Min(fighter.DamageDealt,100)/10)));
        var ranged=fighter.RangedDamage is {} damage?Math.Clamp(damage/fighter.DamageDealt,0,1):fighter.Weapon==ResourceKind.Bow?1:0;
        var dexterity=(int)Math.Round(total*(.3+.5*ranged),MidpointRounding.AwayFromZero);
        citizen.Renown+=Progression.AddExperience(citizen.Stats,Stat.Strength,total-dexterity).RenownGained;
        citizen.Renown+=Progression.AddExperience(citizen.Stats,Stat.Dexterity,dexterity).RenownGained;
        SkillRules.CatchUp(citizen);
    }
}
