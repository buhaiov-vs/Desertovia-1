using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

public static class RuinRules
{
    public const string RoyalBridge="project:tsar-bridge";
    public static TilePoint TargetYard(string plot)=>plot==RoyalBridge?new(27.5,14.5):WorldMap.Yard(plot);

    public static void Collapse(Village village,string plot,IReadOnlyList<Citizen> people,DateTimeOffset at,Guid battleId)
    {
        if(CombatRules.DamageOf(village,plot)<100)return;
        if(plot==RoyalBridge){village.BridgeRuinedAt??=at;village.BridgeOpened=false;return;}
        var building=village.Structures.FirstOrDefault(s=>s.PlotId==plot);
        var storage=village.Storages.FirstOrDefault(s=>s.PlotId==plot);
        var site=village.Sites.FirstOrDefault(s=>s.PlotId==plot);
        if(building is not null)building.RuinedAt??=at;
        if(storage is not null)storage.RuinedAt??=at;
        if(site is not null){site.RuinedAt??=at;site.Damage=100;}
        foreach(var c in people)
        {
            if(c.HomePlotId==plot){c.FlushEnergy(at,village);c.HomePlotId=null;c.ShelterQuality=GameRules.UnderTreesRegeneration;}
            if(c.CurrentSiteId==plot && !c.CycleSettled)
            {
                // Hauling consumes at settlement, so no goods have been lost or need refunding.
                c.IdleSpot=c.Trip?.PositionAt(at)??c.Spot??c.IdleSpot;c.Trip=null;
                c.CurrentSiteId=null;c.CurrentSiteStage=null;c.ReservedSiteWork=null;c.CurrentTaskId=null;
                c.CycleSettled=true;c.CycleEndsAt=null;c.CycleStartedAt=null;c.Spot=null;
            }
            if(building?.Kind==BuildingKind.Hospital && (c.HospitalizedAt is not null||HospitalRules.IsAdmitted(c,at)))
            {c.IdleSpot=RescueRules.Ward(village);c.Trip=null;}
        }
        if(building?.Kind==BuildingKind.Hospital && village.TsarInjuredUntil>at){village.TsarSpot=RescueRules.Ward(village);village.TsarTrip=null;}
        if(building?.Kind==BuildingKind.Castle && building.OwnerAccountId is null && village.DefeatedAt is null)
        {village.DefeatedAt=at;village.DefeatedByBattleId=battleId;}
    }

    public static void Restored(Village village,string plot)
    {
        if(CombatRules.DamageOf(village,plot)>0)return;
        if(plot==RoyalBridge){village.BridgeRuinedAt=null;village.BridgeOpened=true;return;}
        var building=village.Structures.FirstOrDefault(s=>s.PlotId==plot);
        if(building is not null)building.RuinedAt=null;
        var storage=village.Storages.FirstOrDefault(s=>s.PlotId==plot);
        if(storage is not null)storage.RuinedAt=null;
        var site=village.Sites.FirstOrDefault(s=>s.PlotId==plot);
        if(site is not null)site.RuinedAt=null;
        if(building?.Kind==BuildingKind.Castle && building.OwnerAccountId is null)
        {village.DefeatedAt=null;village.DefeatedByBattleId=null;}
    }
}
