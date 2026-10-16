using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

public sealed record RaidReadiness(int Guards,int Reserves,double Power,double Budget);

public static class RaidBalance
{
    public const int MaximumUnits=48;
    public static RaidReadiness Assess(Village village,IReadOnlyList<Citizen> citizens,IReadOnlySet<Guid> online,DateTimeOffset now,int waves=1)
    {
        var healthy=citizens.Where(c=>c.Status==CitizenStatus.Active && c.RecoveryPausedAt is null
            && !HospitalRules.IsAdmitted(c,now) && !CombatRules.IsInjured(c,now) && !(c.ExhaustedUntil>now) && c.Recovery?.Available!=false && c.DungeonRunId is null).ToList();
        var guards=healthy.Where(c=>online.Contains(c.Id)).ToList();
        // Readiness uses present capable residents, never a retired duty assignment.
        var readiness=guards.Sum(c=>CombatRules.Power(c)*(c.CurrentEnergy(now,village)>0?1:.65))
            *(village.Has(BuildingKind.Barracks)?RaidRules.BarracksMultiplier:1);
        var storedWeapons=ArmoryCatalog.Weapons.Sum(w=>Math.Min(guards.Count,village.Stock(w.Kind))*w.Power);
        readiness+=Math.Min(guards.Count*2,storedWeapons*.25);
        var shelter=village.Fortifications.Count(f=>f.Blocks)>0?6:0;
        var scale=waves switch{>=3=>1.2,2=>1.05,_=>.9};
        var budget=Math.Clamp((readiness+6+shelter)*scale,8,240);
        return new(guards.Count,0,readiness,budget);
    }
    public static IReadOnlyList<RaidApproach> Approaches(Village village)
    {
        var nav=new RaidNavigation(village);
        var result=new List<RaidApproach>();
        foreach(var sector in RaidTactics.Sectors)
        {
            try
            {
                var entry=nav.Entry(sector);
                if(nav.Next(entry,WorldMap.Places[WorldMap.Camp],true) is not null)
                    result.Add(new(sector,[new(){X=(int)Math.Floor(entry.X),Y=(int)Math.Floor(entry.Y)}]));
            }
            catch(GameRuleException) { /* A blocked side is not a reason to discard other valid approaches. */ }
        }
        return result;
    }
    public static string Variant(string composition,int index) => composition=="swarm"?"weak":composition=="elite"?"strong":index%4==0?"strong":index%3==0?"weak":"regular";
    private static string AffordableVariant(EnemyKind enemy,string composition,int index,double remaining)
    {
        var desired=Variant(composition,index);
        if(index>0||EnemyCatalog.Get(enemy).Power*Math.Pow(Factor(desired),2)<=remaining)return desired;
        return EnemyCatalog.Get(enemy).Power<=remaining?"regular":"weak";
    }
    public static double Factor(string variant)=>variant=="strong"?1.7:variant=="weak"?.65:1;
    public static int Count(EnemyKind enemy,string composition,double budget)
    {
        var count=0;var remaining=budget;
        while(count<MaximumUnits)
        {
            var cost=EnemyCatalog.Get(enemy).Power*Math.Pow(Factor(AffordableVariant(enemy,composition,count,remaining)),2);
            if(remaining<cost&&count>0)break;
            count++;remaining-=cost;
        }
        return count;
    }
    public static RaidBattle Prepare(Village village,Raid raid,IReadOnlyList<Citizen> citizens,
        IReadOnlyCollection<string> completed,IReadOnlySet<Guid> online,DateTimeOffset now,string? sector=null,string? composition=null)
    {
        Fortifications.Ensure(village,completed);
        var readiness=Assess(village,citizens,online,now,raid.Waves);
        var budget=readiness.Budget;
        var nav=new RaidNavigation(village);
        var choices=Approaches(village).Select(a=>new{a.Sector,Entry=nav.Entry(a.Sector)}).ToList();
        if(choices.Count==0)throw new GameRuleException("raid.noApproach","No reachable approach for this raid.");
        var seed=BitConverter.ToInt32(raid.Id.ToByteArray(),0)&int.MaxValue;
        var chosen=sector is null?choices[seed%choices.Count]:choices.FirstOrDefault(s=>s.Sector==sector)
            ??throw new GameRuleException("raid.noApproach","The selected approach is no longer reachable.");
        var battle=new RaidBattle{Id=raid.Id,VillageId=village.Id,Sector=chosen.Sector,Seed=seed,
            ScheduledAt=raid.StrikesAt,StartsAt=raid.StrikesAt,LastTickAt=raid.StrikesAt,EndsAt=raid.StrikesAt.AddMinutes(4),
            ThreatBudget=budget,ReadyDefenders=readiness.Guards,AvailableReserves=readiness.Reserves,
            Composition=composition is "swarm" or "elite" or "mixed"?composition:new[]{"swarm","elite","mixed"}[seed%3],Tactic=seed%2==0?"entry":"breach"};
        var baseCost=EnemyCatalog.Get(raid.Enemy).Power;
        var remaining=budget;
        while(battle.Units.Count<MaximumUnits)
        {
            var i=battle.Units.Count;
            var variant=AffordableVariant(raid.Enemy,battle.Composition,i,remaining);
            var factor=Factor(variant);
            var cost=baseCost*factor*factor;
            if(remaining<cost && i>0)break;
            var offset=(i%7-3)*.8;
            var row=(i/7)*.8;
            var spawn=chosen.Sector=="west"?new TilePoint(chosen.Entry.X-row,chosen.Entry.Y+offset)
                :new TilePoint(chosen.Entry.X+offset,chosen.Entry.Y+(chosen.Sector=="north"?-row:row));
            var p=nav.NearestFree(spawn,p=>nav.IsOutside(p,chosen.Sector) && battle.Units.All(u=>u.Position.DistanceTo(p)>=.7));
            battle.Units.Add(new(){Variant=variant,X=p.X,Y=p.Y,SpawnX=p.X,SpawnY=p.Y,
                Health=(12+baseCost*2)*factor,MaximumHealth=(12+baseCost*2)*factor,
                Attack=(1.5+baseCost*.4)*factor,Speed=raid.Enemy==EnemyKind.Zombie?.55:.75,
                NextAttackAt=raid.StrikesAt.AddSeconds(2+i%3)});
            remaining-=cost;
        }
        raid.EngineVersion=2;raid.Count=battle.Units.Count;raid.EntrySector=chosen.Sector;
        raid.ApproachPath=[]; // Old visual interpolation must not run over live server actors.
        return battle;
    }
}
