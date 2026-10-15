using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.World;
namespace Desertod.Domain.Rules;

public static class RescueRules
{
    public const string TaskId="rescue-wounded";
    public static TilePoint Ward(Village village) => village.Structures.FirstOrDefault(s=>s.Kind==BuildingKind.Hospital) is {} hospital
        ? WorldMap.Yard(hospital.PlotId) : WorldMap.Places[WorldMap.Camp];
    public static bool NeedsHelp(Citizen patient,DateTimeOffset now) => CombatRules.IsInjured(patient,now)&&patient.HospitalizedAt is null&&!HospitalRules.IsAdmitted(patient,now);
    public static void Check(Citizen helper,Citizen patient,DateTimeOffset now)
    {
        if(helper.Id==patient.Id || helper.VillageId!=patient.VillageId)throw new GameRuleException("rescue.invalid","Choose another wounded citizen of your village.");
        if(!NeedsHelp(patient,now))throw new GameRuleException("rescue.notNeeded","This citizen no longer needs carrying.");
        if(patient.RescueByCitizenId is not null)throw new GameRuleException("rescue.claimed","Someone is already carrying this citizen.");
        if(helper.Load.Any(g=>g.Amount>0))throw new GameRuleException("rescue.hands","Unload your hands before carrying someone.");
    }
    public static void Admit(Citizen patient,Village village,DateTimeOffset now)
    {
        patient.HospitalizedAt=now;patient.RescueByCitizenId=null;patient.IdleSpot=Ward(village);patient.Trip=null;
        // Transport does not heal, change the original deadline or erase the interrupted character's items.
    }
}
