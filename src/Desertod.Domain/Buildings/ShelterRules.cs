using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
namespace Desertod.Domain.Buildings;

public static class ShelterRules
{
    public static int Beds(Structure building) => building.Decommissioning || building.RuinedAt is not null || building.Damage>=100?0:
        building.Kind==BuildingKind.Longhouse ? 12+8*(Math.Clamp(building.Level,1,3)-1) : BuildingCatalog.Get(building.Kind).Shelter;

    /// <summary>Keep valid homes stable; allocate only vacant beds. Never evict for a newer upgrade.</summary>
    public static void Assign(Village village,IEnumerable<Citizen> citizens,DateTimeOffset now)
    {
        var people=citizens.Where(c=>c.Status==CitizenStatus.Active).OrderBy(c=>c.CreatedAt).ThenBy(c=>c.Id).ToList();
        var homes=village.Structures.Where(s=>Beds(s)>0).OrderByDescending(s=>BuildingCatalog.Get(s.Kind).ShelterQuality).ToList();
        var occupied=homes.ToDictionary(s=>s.PlotId,_=>0);
        foreach(var person in people)
        {
            var home=homes.SingleOrDefault(h=>h.PlotId==person.HomePlotId && (h.OwnerAccountId == null || h.OwnerAccountId == person.AccountId));
            if(home is null||occupied[home.PlotId]>=Beds(home)) person.HomePlotId=null;
            else occupied[home.PlotId]++;
        }
        foreach(var person in people)
        {
            if(person.HomePlotId is null&&homes.FirstOrDefault(h=>(h.OwnerAccountId == null || h.OwnerAccountId == person.AccountId) && occupied[h.PlotId]<Beds(h)) is {} home)
            {person.HomePlotId=home.PlotId;occupied[home.PlotId]++;}
            var assigned=homes.SingleOrDefault(h=>h.PlotId==person.HomePlotId && (h.OwnerAccountId == null || h.OwnerAccountId == person.AccountId));
            var quality=assigned is null?GameRules.UnderTreesRegeneration:BuildingCatalog.Get(assigned.Kind).ShelterQuality;
            if(Math.Abs(person.ShelterQuality-quality)>1e-9)
            {person.FlushEnergy(now,village);person.ShelterQuality=quality;}
        }
    }

    public static Dictionary<Guid,string> Relocation(Village village,IReadOnlyList<Citizen> citizens,string excluded)
    {
        var homes=village.Structures.Where(h=>h.PlotId!=excluded&&Beds(h)>0).OrderByDescending(h=>BuildingCatalog.Get(h.Kind).ShelterQuality).ToList();
        var remaining=homes.ToDictionary(h=>h.PlotId,h=>Math.Max(0,Beds(h)-citizens.Count(c=>c.HomePlotId==h.PlotId)));
        var result=new Dictionary<Guid,string>();
        // Owners first: their private beds cannot house anyone else.
        foreach(var person in citizens.Where(c=>c.HomePlotId==excluded).OrderByDescending(c=>homes.Any(h=>h.OwnerAccountId==c.AccountId)).ThenBy(c=>c.Id))
        {
            var home=homes.OrderByDescending(h=>h.OwnerAccountId==person.AccountId).FirstOrDefault(h=>remaining[h.PlotId]>0&&(h.OwnerAccountId is null||h.OwnerAccountId==person.AccountId));
            if(home is null)continue;
            remaining[home.PlotId]--;result[person.Id]=home.PlotId;
        }
        return result;
    }

    public static void RequireRelocation(Village village,IReadOnlyList<Citizen> citizens,string plot)
    {
        var needed=citizens.Count(c=>c.HomePlotId==plot);var available=Relocation(village,citizens,plot).Count;
        if(available<needed)throw new GameRuleException("housing.relocateFirst","Provide suitable free beds and relocate the residents first.",
            new Dictionary<string,object>{{"needed",needed},{"available",available}});
    }

    public static void Move(Village village,IReadOnlyList<Citizen> citizens,IReadOnlyList<Citizen> moving,string plot,DateTimeOffset now)
    {
        var home=village.Structures.SingleOrDefault(s=>s.PlotId==plot&&Beds(s)>0)
            ??throw new GameRuleException("housing.notFound","Select a completed residential building.");
        if(home.OwnerAccountId is {} owner && moving.Any(c => c.AccountId != owner))
            throw new GameRuleException("housing.private", "This home belongs to another resident.");
        if(citizens.Count(c=>c.HomePlotId==plot&&!moving.Contains(c))+moving.Count>Beds(home))
            throw new GameRuleException("housing.full","Not enough free beds for this move.");
        foreach(var person in moving){person.FlushEnergy(now,village);person.HomePlotId=plot;person.ShelterQuality=BuildingCatalog.Get(home.Kind).ShelterQuality;}
    }
}
