using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
namespace Desertod.Domain.Buildings;

public static class ShelterRules
{
    public static int Beds(Structure building) => building.Decommissioning?0:
        building.Kind==BuildingKind.Longhouse ? 12+8*(Math.Clamp(building.Level,1,3)-1) : BuildingCatalog.Get(building.Kind).Shelter;

    /// <summary>Keep valid homes stable; allocate only vacant beds. Never evict for a newer upgrade.</summary>
    public static void Assign(Village village,IEnumerable<Citizen> citizens,DateTimeOffset now)
    {
        var people=citizens.Where(c=>c.Status==CitizenStatus.Active).OrderBy(c=>c.CreatedAt).ThenBy(c=>c.Id).ToList();
        var homes=village.Structures.Where(s=>Beds(s)>0).OrderByDescending(s=>BuildingCatalog.Get(s.Kind).ShelterQuality).ToList();
        var occupied=homes.ToDictionary(s=>s.PlotId,_=>0);
        foreach(var person in people)
        {
            var home=homes.SingleOrDefault(h=>h.PlotId==person.HomePlotId);
            if(home is null||occupied[home.PlotId]>=Beds(home)) person.HomePlotId=null;
            else occupied[home.PlotId]++;
        }
        foreach(var person in people)
        {
            if(person.HomePlotId is null&&homes.FirstOrDefault(h=>occupied[h.PlotId]<Beds(h)) is {} home)
            {person.HomePlotId=home.PlotId;occupied[home.PlotId]++;}
            var assigned=homes.SingleOrDefault(h=>h.PlotId==person.HomePlotId);
            var quality=assigned is null?GameRules.UnderTreesRegeneration:BuildingCatalog.Get(assigned.Kind).ShelterQuality;
            if(Math.Abs(person.ShelterQuality-quality)>1e-9)
            {person.FlushEnergy(now,village);person.ShelterQuality=quality;}
        }
    }

    public static void Move(Village village,IReadOnlyList<Citizen> citizens,IReadOnlyList<Citizen> moving,string plot,DateTimeOffset now)
    {
        var home=village.Structures.SingleOrDefault(s=>s.PlotId==plot&&Beds(s)>0)
            ??throw new GameRuleException("housing.notFound","Select a completed residential building.");
        if(citizens.Count(c=>c.HomePlotId==plot&&!moving.Contains(c))+moving.Count>Beds(home))
            throw new GameRuleException("housing.full","Not enough free beds for this move.");
        foreach(var person in moving){person.FlushEnergy(now,village);person.HomePlotId=plot;person.ShelterQuality=BuildingCatalog.Get(home.Kind).ShelterQuality;}
    }
}
