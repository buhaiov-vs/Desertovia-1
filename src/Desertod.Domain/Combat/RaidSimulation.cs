using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

/// <summary>Authoritative, bounded steps. No aggregate victory calculation and no network/model calls.</summary>
public static class RaidSimulation
{
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(1);
    public const int MaximumCatchupSteps = 3;
    public const int PlunderDamageLimit = 180;

    public static bool Tick(Village village,Raid raid,RaidBattle battle,IReadOnlyList<Citizen> people,
        IReadOnlySet<Guid> online,DateTimeOffset now)
    {
        if(battle.FinishedAt is not null || now < battle.StartsAt || now <= battle.LastTickAt)return false;
        // A server outage freezes the fight rather than charging hours of unseen attacks to players.
        if(now-battle.LastTickAt>TimeSpan.FromSeconds(15))
        {
            var pause=now-battle.LastTickAt-Step;
            battle.EndsAt+=pause;battle.LastTickAt+=pause;
            foreach(var u in battle.Units)u.NextAttackAt+=pause;
            foreach(var f in battle.Fighters)f.NextAttackAt+=pause;
            battle.Event("resumed","",now);
        }
        var steps=Math.Min(MaximumCatchupSteps,(int)Math.Floor((now-battle.LastTickAt).TotalSeconds));
        if(steps==0)return false;
        var nav=new RaidNavigation(village);
        var citizens=people.ToDictionary(c=>c.Id);
        for(var step=0;step<steps;step++)
        {
            var at=battle.LastTickAt+Step;
            Join(battle,people,online,at,nav);
            if(battle.Phase=="Warning")
            {battle.Phase="Approaching";battle.Event("arrived",battle.Sector,at);foreach(var u in battle.Units)u.State="Approaching";}
            var living=battle.Units.Count(u=>u.Alive);
            if(at>=battle.EndsAt || battle.DamageDone>=PlunderDamageLimit || living<=battle.Units.Count*.3)
            {
                if(battle.Phase!="Retreating")battle.Event("retreat",battle.Sector,at);
                battle.Phase="Retreating";
            }
            foreach(var f in battle.Fighters.Where(f=>f.Active&&f.Guard&&f.Health>0))
                if(citizens.TryGetValue(f.CitizenId,out var c))Guard(village,raid,battle,f,c,nav,at);
            foreach(var unit in battle.Units.Where(u=>u.Alive))
                Enemy(village,raid,battle,unit,citizens,nav,at);
            battle.LastTickAt=at;battle.Revision++;
            if(battle.Units.All(u=>!u.Alive))
            {
                Finish(village,raid,battle,citizens,at);
                break;
            }
        }
        return true;
    }

    private static bool Available(Citizen c,DateTimeOffset at) => c.Status==CitizenStatus.Active
        && c.RecoveryPausedAt is null && !HospitalRules.IsAdmitted(c,at) && !(c.ExhaustedUntil>at)
        && c.DungeonRunId is null;

    private static void Join(RaidBattle battle,IReadOnlyList<Citizen> people,IReadOnlySet<Guid> online,
        DateTimeOffset at,RaidNavigation nav)
    {
        foreach(var c in people)
        {
            var guard=Available(c,at)&&RaidRules.GuardTask(c,at)!=null;
            var position=c.Spot??c.IdleSpot??WorldMap.Places[WorldMap.Camp];
            var inDanger=online.Contains(c.Id)&&Available(c,at)&&battle.Units.Any(u=>u.Alive&&u.Position.DistanceTo(position)<4);
            var existing=battle.Fighters.SingleOrDefault(f=>f.CitizenId==c.Id);
            if(existing is not null)
            {existing.Guard=guard;existing.Active=Available(c,at)&&(guard||inDanger);continue;}
            if(!guard&&!inDanger)continue;
            var p=nav.NearestFree(position);
            battle.Fighters.Add(new(){CitizenId=c.Id,X=p.X,Y=p.Y,Guard=guard,Active=true,
                Health=32+c.Stats.Endurance*4,MaximumHealth=32+c.Stats.Endurance*4,NextAttackAt=at.AddSeconds(1)});
        }
    }

    private static void Guard(Village village,Raid raid,RaidBattle battle,RaidFighter fighter,
        Citizen citizen,RaidNavigation nav,DateTimeOffset at)
    {
        if(citizen.DefenseReadyAt>at)return;
        var home=nav.NearestFree(RaidNavigation.GuardPoint(citizen.DefenseSector));
        var weapon=CombatRules.UsableWeapon(citizen);
        var range=weapon?.Kind==ResourceKind.Bow?5:1.45;
        var target=battle.Units.FirstOrDefault(u=>u.Id==fighter.TargetUnitId&&u.Alive);
        if(target is not null&&target.Position.DistanceTo(home)>12){fighter.TargetUnitId=null;target=null;}
        target??=battle.Units.Where(u=>u.Alive&&u.Position.DistanceTo(home)<(fighter.Mode=="hold"?4:10))
            .OrderBy(u=>u.Position.DistanceTo(fighter.Position)).FirstOrDefault();
        if(target is null){nav.Move(fighter,home,1);return;}
        var distance=target.Position.DistanceTo(fighter.Position);
        if(distance>range || !nav.ClearShot(fighter.Position,target.Position))
        {
            if(fighter.Mode!="hold"||fighter.Position.DistanceTo(home)>1)nav.Move(fighter,fighter.Mode=="hold"?home:target.Position,1);
            return;
        }
        if(fighter.NextAttackAt>at)return;
        fighter.NextAttackAt=at.AddSeconds(weapon?.Kind==ResourceKind.Bow?2.5:2);
        if(weapon?.Arrows>0)citizen.Arrows=Math.Max(0,citizen.Arrows-1);
        var hit=Math.Min(target.Health,Math.Max(1,CombatRules.Power(citizen)*.85));
        target.Health=Math.Max(0,target.Health-hit);fighter.DamageDealt+=hit;
        if(battle.Phase=="Approaching")battle.Phase="Fighting";
        if(target.Health>0)return;
        target.State="Dead";target.DiedAt=at;fighter.Kills++;
        battle.Event("enemy_down",target.Id.ToString(),at);
    }

    private static void Enemy(Village village,Raid raid,RaidBattle battle,RaidUnit unit,
        IReadOnlyDictionary<Guid,Citizen> people,RaidNavigation nav,DateTimeOffset at)
    {
        var enemy=EnemyCatalog.Get(raid.Enemy);
        if(battle.Phase=="Retreating"||unit.State=="Retreating")
        {
            unit.State="Retreating";
            var exit=new TilePoint(unit.SpawnX,unit.SpawnY);
            if(unit.Position.DistanceTo(exit)<1 || at>battle.EndsAt.AddSeconds(60))
            {
                Escape(village,raid,unit,enemy);unit.ExitedAt=at;unit.State="Escaped";return;
            }
            if(!nav.Move(unit,exit,1,enemy.Damage>0,true,out var blocked))unit.BlockedSeconds++;
            if(blocked is not null)BreakWall(unit,blocked,battle,enemy,at);
            if(unit.BlockedSeconds>12){unit.ExitedAt=at;unit.State="Escaped";}
            return;
        }
        var near=battle.Fighters.Where(f=>f.Active&&f.Health>0&&unit.Position.DistanceTo(f.Position)<=4)
            .OrderBy(f=>unit.Position.DistanceTo(f.Position)+(battle.Fighters.Count(a=>a.Active&&a.CitizenId.ToString()==unit.TargetId)>2?1:0)).ToList();
        var victim=near.FirstOrDefault();
        if(victim is not null && (enemy.Coordinated || unit.Position.DistanceTo(victim.Position)<2.5))
        {
            unit.TargetKind="citizen";unit.TargetId=victim.CitizenId.ToString();unit.State="Fighting";battle.Phase="Fighting";
            if(unit.Position.DistanceTo(victim.Position)>unit.Range || !nav.ClearShot(unit.Position,victim.Position))
            {
                nav.Move(unit,victim.Position,1,enemy.Damage>0,battle.Tactic=="breach",out var barrier);
                if(barrier is not null)BreakWall(unit,barrier,battle,enemy,at);
                return;
            }
            if(unit.NextAttackAt>at)return;
            unit.NextAttackAt=at.AddSeconds(3);
            if(!people.TryGetValue(victim.CitizenId,out var c))return;
            var protection=ArmoryCatalog.Armor(c.Armor)?.Protection??0;
            victim.Health=Math.Max(0,victim.Health-unit.Attack*(1-protection));
            if(victim.Health<=0)
            {
                victim.Active=false;victim.Wounded=true;
                CombatRules.Injure(c,TimeSpan.FromMinutes(enemy.InjuryMinutes),at);
                c.IdleSpot=victim.Position;
                battle.Event("wounded",c.Id.ToString(),at);
            }
            return;
        }
        var index=battle.Units.IndexOf(unit);
        var centre=WorldMap.Places[WorldMap.Camp];
        var inVillage=KingdomRules.Area(village).Contains(unit.Position.ToTile());
        // A minority may raid nearby outbuildings. Others keep advancing; no single-shed decoy for the whole horde.
        var canDistract=index%4==0 || unit.Position.DistanceTo(centre)<7;
        var building=canDistract ? Targets(village).Where(p=>CombatRules.DamageOf(village,p)<CombatRules.MaximumDamage)
            .Where(p=>WorldMap.Yard(p).DistanceTo(unit.Position)<(inVillage?6:4))
            .Where(p=>inVillage||!KingdomRules.Area(village).Overlaps(WorldMap.Footprint(p)!.Value))
            .Where(p=>battle.Units.Count(u=>u.Alive&&u.TargetKind=="building"&&u.TargetId==p)<3||unit.TargetId==p)
            .OrderBy(p=>WorldMap.Yard(p).DistanceTo(unit.Position)).FirstOrDefault():null;
        var goal=centre;
        if(building is not null && enemy.Damage>0)
        {
            goal=WorldMap.Yard(building);unit.TargetKind="building";unit.TargetId=building;
            if(unit.Position.DistanceTo(goal)<1.4)
            {
                unit.State="Raiding";battle.Phase="Raiding";
                if(unit.NextAttackAt>at)return;
                unit.NextAttackAt=at.AddSeconds(3);
                var added=CombatRules.Damage(village,building,Math.Max(1,(int)Math.Round(enemy.Damage*.25*(unit.Variant=="strong"?1.5:1))));
                battle.DamageDone+=added;
                var damaged=battle.Damage.FirstOrDefault(d=>d.PlotId==building);
                if(damaged is null)battle.Damage.Add(new(){PlotId=building,Amount=added});else damaged.Amount+=added;
                if(unit.PlunderPlot is null && village.Storages.Any(s=>s.PlotId==building&&s.OwnerAccountId is null))unit.PlunderPlot=building;
                if(CombatRules.DamageOf(village,building)>=50 && unit.PlunderPlot is not null)unit.State="Retreating";
                return;
            }
        }
        else{unit.TargetKind="centre";unit.TargetId=null;}
        unit.State="Approaching";
        if(unit.Position.DistanceTo(goal)<1 && building is null)
        {
            // At the rally point choose an actual building to raid; the fire itself is never attacked.
            var p=Targets(village).Where(p=>CombatRules.DamageOf(village,p)<100).OrderBy(p=>WorldMap.Yard(p).DistanceTo(unit.Position)).FirstOrDefault();
            if(p is not null)goal=WorldMap.Yard(p);else{unit.State="Retreating";return;}
        }
        var moved=nav.Move(unit,goal,1,enemy.Damage>0,battle.Tactic=="breach",out var wall);
        if(wall is not null)BreakWall(unit,wall,battle,enemy,at);
        unit.BlockedSeconds=moved?0:unit.BlockedSeconds+1;
        if(unit.BlockedSeconds>=10)unit.State="Retreating";
    }

    private static IEnumerable<string> Targets(Village v) => v.Structures.Where(s=>!s.Decommissioning).Select(s=>s.PlotId)
        .Concat(v.Storages.Where(s=>!s.Decommissioning).Select(s=>s.PlotId));

    private static void BreakWall(RaidUnit unit,Fortification wall,RaidBattle battle,EnemyDefinition enemy,DateTimeOffset at)
    {
        unit.State="Breaching";unit.TargetKind="wall";unit.TargetId=wall.Id;
        if(unit.NextAttackAt>at)return;
        unit.NextAttackAt=at.AddSeconds(3);
        wall.Damage=Math.Min(100,wall.Damage+Math.Max(1,(int)Math.Round(enemy.Damage*.35*(unit.Variant=="strong"?1.5:1))));
        if(wall.Damage>=100)battle.Event("breach",wall.Id,at);
    }

    private static void Escape(Village village,Raid raid,RaidUnit unit,EnemyDefinition enemy)
    {
        // Loot stays in its source until a living raider escapes. A killed carrier never creates a second copy.
        var store=village.Storages.FirstOrDefault(s=>s.PlotId==unit.PlunderPlot&&s.OwnerAccountId is null);
        if(store is null)return;
        foreach(var good in enemy.Steals)
        {
            var taken=store.Take(good.Resource,Math.Min(good.Amount,4));
            if(taken<=0)continue;
            var existing=raid.Stolen.FirstOrDefault(g=>g.Kind==good.Resource);
            if(existing is null)raid.Stolen.Add(new(){Kind=good.Resource,Amount=taken});else existing.Amount+=taken;
        }
    }

    private static void Finish(Village village,Raid raid,RaidBattle battle,IReadOnlyDictionary<Guid,Citizen> people,DateTimeOffset at)
    {
        battle.Phase="Finished";battle.FinishedAt=at;
        raid.Killed=battle.Units.Count(u=>u.Health<=0);
        raid.Defenders=battle.Fighters.Count(f=>f.DamageDealt>0);
        raid.Injured=battle.Fighters.Count(f=>f.Wounded);
        raid.DamagedBuildings=battle.Damage.Count(d=>d.Amount>0);
        raid.ResolvedAt=at;
        raid.Outcome=battle.DamageDone<50&&raid.Stolen.Count==0?RaidOutcome.Repelled:RaidOutcome.Lost;
        var contributors=battle.Fighters.Where(f=>f.DamageDealt>0&&!f.RewardsGranted).OrderBy(f=>f.CitizenId).ToList();
        var total=contributors.Sum(f=>f.DamageDealt);
        var remaining=raid.Killed*RaidRules.RenownPerKill;
        for(var i=0;i<contributors.Count;i++)
        {
            var f=contributors[i];if(!people.TryGetValue(f.CitizenId,out var c))continue;
            var reward=i==contributors.Count-1?remaining:(int)Math.Floor(raid.Killed*RaidRules.RenownPerKill*f.DamageDealt/Math.Max(1,total));
            remaining-=reward;c.Renown+=reward;
            SkillRules.AddExperience(c,Skill.Guarding,CombatRules.FightExperience);
            // Equipment wears once per encounter, not once per server tick. Arrows were consumed by actual shots.
            var random=new Random(battle.Seed^c.Id.GetHashCode());
            if(c.Weapon is {} w && random.NextDouble()<(ArmoryCatalog.Weapon(w)?.BreakChance??0))c.Weapon=null;
            if(c.Armor is {} a && random.NextDouble()<(ArmoryCatalog.Armor(a)?.BreakChance??0))c.Armor=null;
            f.RewardsGranted=true;
        }
        foreach(var good in EnemyCatalog.Get(raid.Enemy).Loot)
        {
            var amount=village.Store(good.Resource,good.Amount*raid.Killed);
            if(amount>0)raid.Loot.Add(new(){Kind=good.Resource,Amount=amount});
        }
        village.AddMorale(raid.Outcome==RaidOutcome.Repelled?RaidRules.RepelledMorale:RaidRules.LostMorale,at);
        battle.Event("finished",raid.Outcome.ToString(),at);
        RaidRules.UpdateNextRaidAt(village);
    }
}
