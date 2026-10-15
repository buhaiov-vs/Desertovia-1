using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

public static class RaidBalance
{
    public const int MaximumUnits=48;
    public static RaidBattle Prepare(Village village,Raid raid,IReadOnlyList<Citizen> citizens,
        IReadOnlyCollection<string> completed,IReadOnlySet<Guid> online,DateTimeOffset now,string? sector=null)
    {
        Fortifications.Ensure(village,completed);
        var healthy=citizens.Where(c=>c.Status==CitizenStatus.Active && c.RecoveryPausedAt is null
            && !HospitalRules.IsAdmitted(c,now) && !(c.ExhaustedUntil>now) && c.DungeonRunId is null).ToList();
        var guards=healthy.Where(c=>RaidRules.GuardTask(c,now)!=null).ToList();
        var reserve=healthy.Where(c=>!guards.Contains(c)&&online.Contains(c.Id)).ToList();
        // Equipped guards count fully. Idle, available residents count only as a bounded reserve, not a full army.
        var readiness=guards.Sum(c=>CombatRules.Power(c)*(c.CurrentEnergy(now,village)>0?1:.65))
            + reserve.OrderByDescending(CombatRules.Power).Take(12).Sum(c=>CombatRules.Power(c)*.4);
        var storedWeapons=ArmoryCatalog.Weapons.Sum(w=>Math.Min(reserve.Count,village.Stock(w.Kind))*w.Power);
        readiness+=Math.Min(reserve.Count*2,storedWeapons*.25);
        var shelter=village.Fortifications.Count(f=>f.Blocks)>0?6:0;
        var scale=raid.Waves switch{>=3=>1.2,2=>1.05,_=>.9};
        var budget=Math.Clamp((readiness+6+shelter)*scale,8,240);
        var nav=new RaidNavigation(village);
        var choices=RaidTactics.Sectors.Select(s=>new{Sector=s,Entry=nav.Entry(s)})
            .Where(p=>nav.Next(p.Entry,WorldMap.Places[WorldMap.Camp],true)!=null).ToList();
        if(choices.Count==0)throw new GameRuleException("raid.noApproach","No reachable approach for this raid.");
        var seed=BitConverter.ToInt32(raid.Id.ToByteArray(),0)&int.MaxValue;
        var chosen=choices.FirstOrDefault(s=>s.Sector==sector)??choices[seed%choices.Count];
        var battle=new RaidBattle{Id=raid.Id,VillageId=village.Id,Sector=chosen.Sector,Seed=seed,
            StartsAt=raid.StrikesAt,LastTickAt=raid.StrikesAt,EndsAt=raid.StrikesAt.AddMinutes(4),
            ThreatBudget=budget,ReadyDefenders=guards.Count,AvailableReserves=reserve.Count,
            Composition=new[]{"swarm","elite","mixed"}[seed%3],Tactic=seed%2==0?"entry":"breach"};
        var baseCost=EnemyCatalog.Get(raid.Enemy).Power;
        var remaining=budget;
        while(battle.Units.Count<MaximumUnits)
        {
            var i=battle.Units.Count;
            var variant=battle.Composition=="swarm"?"weak":battle.Composition=="elite"?"strong":i%4==0?"strong":i%3==0?"weak":"regular";
            var factor=variant=="strong"?1.7:variant=="weak"?.65:1;
            var cost=baseCost*factor*factor;
            if(remaining<cost && i>0)break;
            var offset=(i%7-3)*.8;
            var row=(i/7)*.8;
            var spawn=chosen.Sector=="west"?new TilePoint(chosen.Entry.X-row,chosen.Entry.Y+offset)
                :new TilePoint(chosen.Entry.X+offset,chosen.Entry.Y+(chosen.Sector=="north"?-row:row));
            var p=nav.NearestFree(spawn);
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
