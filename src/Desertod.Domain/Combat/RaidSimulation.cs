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
                if(f.IsTsar) ActTsar(village,battle,f,nav,at);
                else if(citizens.TryGetValue(f.CitizenId,out var c))Guard(village,raid,battle,f,c,citizens,nav,at);
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
        && c.RecoveryPausedAt is null && !HospitalRules.IsAdmitted(c,at) && !CombatRules.IsInjured(c,at) && !(c.ExhaustedUntil>at)
        && c.Recovery?.Available!=false && c.DungeonRunId is null;

    private static void Join(RaidBattle battle,IReadOnlyList<Citizen> people,IReadOnlySet<Guid> online,
        DateTimeOffset at,RaidNavigation nav)
    {
        foreach(var c in people)
        {
            if (battle.Fighters.Any(f => f.IsTsar && f.AccountId == c.AccountId)) continue;
            var guard=Available(c,at)&&RaidRules.GuardTask(c,at)!=null;
            var position=c.Spot??c.IdleSpot??WorldMap.Places[WorldMap.Camp];
            var inDanger=online.Contains(c.Id)&&Available(c,at)&&battle.Units.Any(u=>u.Alive&&u.Position.DistanceTo(position)<4);
            var existing=battle.Fighters.SingleOrDefault(f=>f.CitizenId==c.Id);
            if(existing is not null)
            {
                existing.Guard=guard;existing.Active=Available(c,at)&&(guard||inDanger);
                existing.Weapon=c.Weapon;existing.Armor=c.Armor;existing.Arrows=c.Arrows;
                if(existing.Wounded&&Available(c,at))
                {existing.Health=existing.MaximumHealth;existing.Wounded=false;existing.X=position.X;existing.Y=position.Y;}
                continue;
            }
            if(!guard&&!inDanger)continue;
            var p=nav.NearestFree(position);
            battle.Fighters.Add(new(){CitizenId=c.Id,X=p.X,Y=p.Y,Guard=guard,Active=true,
                Health=32+c.Stats.Endurance*4,MaximumHealth=32+c.Stats.Endurance*4,Weapon=c.Weapon,Armor=c.Armor,Arrows=c.Arrows,NextAttackAt=at.AddSeconds(1)});
        }
    }

    private static void Guard(Village village,Raid raid,RaidBattle battle,RaidFighter fighter,
        Citizen citizen,IReadOnlyDictionary<Guid,Citizen> battlePeople,RaidNavigation nav,DateTimeOffset at)
    {
        if(citizen.DefenseReadyAt>at)return;
        if (MoveOrder(fighter, nav)) return;
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
        fighter.Arrows=citizen.Arrows;
        var captain=battle.Fighters.Where(f=>f.Guard&&f.Active&&f.Health>0&&f.Position.DistanceTo(fighter.Position)<=5)
            .Select(f=>battlePeople.GetValueOrDefault(f.CitizenId))
            .Where(c=>c is not null&&RaidRules.GuardTask(c,at)?.RoleId==RaidRules.CaptainRole)
            .Select(c=>1+RaidRules.CommandPerEndurance*c!.Stats.Endurance).DefaultIfEmpty(1).Max();
        var multiplier=(RaidRules.GuardTask(citizen,at)?.BaseYield??1)*captain*(village.Has(BuildingKind.Barracks)?RaidRules.BarracksMultiplier:1);
        var hit=Math.Min(target.Health,Math.Max(1,CombatRules.Power(citizen)*multiplier*.85));
        target.Health=Math.Max(0,target.Health-hit);fighter.DamageDealt+=hit;
        if(battle.Phase=="Approaching")battle.Phase="Fighting";
        if(target.Health>0)return;
        target.State="Dead";target.DiedAt=at;fighter.Kills++;
        battle.Event("enemy_down",target.Id.ToString(),at);
    }

    private static bool MoveOrder(RaidFighter fighter, RaidNavigation nav)
    {
        if (fighter.DestinationX is not {} x || fighter.DestinationY is not {} y) return false;
        var goal = new TilePoint(x,y);
        nav.Move(fighter,goal,1);
        // A move order stays authoritative until another command: reaching the point
        // must not immediately turn a deliberate retreat into an automatic chase.
        return true;
    }

    private static void ActTsar(Village village, RaidBattle battle, RaidFighter fighter, RaidNavigation nav, DateTimeOffset at)
    {
        if (fighter.AccountId != village.OwnerAccountId || village.TsarInjuredUntil > at)
        { fighter.Active=false; return; }
        if (MoveOrder(fighter,nav)) { village.TsarSpot=fighter.Position; return; }
        var target=battle.Units.FirstOrDefault(u=>u.Id==fighter.TargetUnitId&&u.Alive);
        target ??= battle.Units.Where(u=>u.Alive&&u.Position.DistanceTo(fighter.Position)<(fighter.Mode=="hold"?4:10))
            .OrderBy(u=>u.Position.DistanceTo(fighter.Position)).FirstOrDefault();
        if (target is null) return;
        if (target.Position.DistanceTo(fighter.Position)>1.0 || !nav.ClearShot(fighter.Position,target.Position))
        { if(fighter.Mode!="hold") nav.Move(fighter,target.Position,1); village.TsarSpot=fighter.Position; return; }
        if (fighter.NextAttackAt>at) return;
        fighter.NextAttackAt=at.AddSeconds(1);
        var hit=Math.Min(target.Health,Desertod.Domain.Dungeons.HeroClassCatalog.VoivodeDamage);
        target.Health=Math.Max(0,target.Health-hit);fighter.DamageDealt+=hit;
        battle.Event("hit",target.Id.ToString(),at);
        if(target.Health<=0){target.State="Dead";target.DiedAt=at;fighter.Kills++;battle.Event("enemy_down",target.Id.ToString(),at);}
        if(battle.Phase=="Approaching")battle.Phase="Fighting";
    }

    private static void Enemy(Village village,Raid raid,RaidBattle battle,RaidUnit unit,
        IReadOnlyDictionary<Guid,Citizen> people,RaidNavigation nav,DateTimeOffset at)
    {
        var enemy=EnemyCatalog.Get(raid.Enemy);
        if(battle.Phase=="Retreating"||unit.State=="Retreating")
        {
            unit.State="Retreating";
            var exit=new TilePoint(unit.SpawnX,unit.SpawnY);
            if(unit.Position.DistanceTo(exit)<1)
            {
                Escape(village,raid,unit,enemy);unit.ExitedAt=at;unit.State="Escaped";return;
            }
            if(!MoveUnit(battle,nav,unit,exit,1,enemy.Damage>0,true,out var blocked))unit.BlockedSeconds++;
            if(blocked is not null)BreakWall(unit,blocked,battle,enemy,at);
            if(at>battle.EndsAt.AddSeconds(60))
            {
                // Trapped raiders disperse without taking goods through an impassable wall.
                unit.ExitedAt=at;unit.State="Dispersed";
            }
            return;
        }
        var near=battle.Fighters.Where(f=>f.Active&&f.Health>0&&unit.Position.DistanceTo(f.Position)<=4)
            .OrderBy(f=>unit.Position.DistanceTo(f.Position)+(battle.Units.Count(a=>a.Alive&&a.TargetId==f.ActorId.ToString())>2?1:0)).ToList();
        var victim=near.FirstOrDefault();
        if(victim is not null && (enemy.Coordinated || unit.Position.DistanceTo(victim.Position)<2.5))
        {
            unit.TargetKind=victim.IsTsar?"tsar":"citizen";unit.TargetId=victim.ActorId.ToString();unit.State="Fighting";battle.Phase="Fighting";
            if(unit.Position.DistanceTo(victim.Position)>unit.Range || !nav.ClearShot(unit.Position,victim.Position))
            {
                MoveUnit(battle,nav,unit,victim.Position,1,enemy.Damage>0,battle.Tactic=="breach",out var barrier);
                if(barrier is not null)BreakWall(unit,barrier,battle,enemy,at);
                return;
            }
            if(unit.NextAttackAt>at)return;
            unit.NextAttackAt=at.AddSeconds(3);
            if (victim.IsTsar)
            {
                victim.Health=Math.Max(0,victim.Health-unit.Attack);
                if(victim.Health<=0)
                {
                    victim.Active=false;victim.Wounded=true;
                    village.TsarInjuredUntil=at.AddMinutes(enemy.InjuryMinutes);
                    village.TsarSpot=RescueRules.Ward(village);village.TsarTrip=null;
                    battle.Event("wounded",victim.ActorId.ToString(),at);
                }
                return;
            }
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
        var canDistract=index%4==0 || unit.Position.DistanceTo(centre)<7 || unit.TargetKind=="building";
        var building=canDistract ? Targets(village).Where(p=>CombatRules.DamageOf(village,p)<CombatRules.MaximumDamage)
            .Where(p=>WorldMap.Yard(p).DistanceTo(unit.Position)<(inVillage?6:4))
            .Where(p=>inVillage||!KingdomRules.Area(village).Overlaps(WorldMap.Footprint(p)!.Value))
            .Where(p=>battle.Units.Count(u=>u.Alive&&u.TargetKind=="building"&&u.TargetId==p)<3||unit.TargetId==p)
            .OrderBy(p=>(unit.TargetKind=="building"&&unit.TargetId==p?-3:0)+WorldMap.Yard(p).DistanceTo(unit.Position)).FirstOrDefault():null;
        var goal=centre;
        if(building is not null)
        {
            goal=WorldMap.Yard(building);unit.TargetKind="building";unit.TargetId=building;
            if(unit.Position.DistanceTo(goal)<1.4 && nav.ClearShot(unit.Position,goal))
            {
                unit.State="Raiding";battle.Phase="Raiding";
                if(unit.NextAttackAt>at)return;
                unit.NextAttackAt=at.AddSeconds(3);
                var damage=Math.Min(PlunderDamageLimit-(int)battle.DamageDone,
                    enemy.Damage==0?0:Math.Max(1,(int)Math.Round(enemy.Damage*.25*(unit.Variant=="strong"?1.5:1))));
                var added=damage>0?CombatRules.Damage(village,building,damage):0;
                battle.DamageDone+=added;
                var damaged=battle.Damage.FirstOrDefault(d=>d.PlotId==building);
                if(damaged is null)battle.Damage.Add(new(){PlotId=building,Amount=added});else damaged.Amount+=added;
                if(unit.PlunderPlot is null && village.Storages.Any(s=>s.PlotId==building&&s.OwnerAccountId is null))unit.PlunderPlot=building;
                if((enemy.Damage==0 || CombatRules.DamageOf(village,building)>=50) && unit.PlunderPlot is not null)unit.State="Retreating";
                return;
            }
        }
        else{unit.TargetKind="centre";unit.TargetId=null;}
        unit.State="Approaching";
        if(unit.Position.DistanceTo(goal)<1 && building is null)
        {
            // At the rally point choose an actual building to raid; the fire itself is never attacked.
            var p=Targets(village).Where(p=>CombatRules.DamageOf(village,p)<100).OrderBy(p=>WorldMap.Yard(p).DistanceTo(unit.Position)).FirstOrDefault();
            if(p is not null){goal=WorldMap.Yard(p);unit.TargetKind="building";unit.TargetId=p;}else{unit.State="Retreating";return;}
        }
        var moved=MoveUnit(battle,nav,unit,goal,1,enemy.Damage>0,battle.Tactic=="breach",out var wall);
        if(wall is not null)BreakWall(unit,wall,battle,enemy,at);
        unit.BlockedSeconds=moved?0:unit.BlockedSeconds+1;
        if(unit.BlockedSeconds>=10)unit.State="Retreating";
    }

    private static bool MoveUnit(RaidBattle battle,RaidNavigation nav,RaidUnit unit,TilePoint goal,double seconds,bool breach,bool prefer,out Fortification? wall)
    {
        var before=unit.Position;
        var moved=nav.Move(unit,goal,seconds,breach,prefer,out wall);
        if(moved && wall is null && battle.Units.Any(other=>other.Id!=unit.Id&&other.Alive&&other.Position.DistanceTo(unit.Position)<.4))
        {
            unit.X=before.X;unit.Y=before.Y;
            var dx=goal.X-before.X;var dy=goal.Y-before.Y;var length=Math.Max(.01,Math.Sqrt(dx*dx+dy*dy));
            foreach(var sign in new[]{1,-1})
            {
                var side=new TilePoint(before.X-sign*dy/length*unit.Speed*seconds,before.Y+sign*dx/length*unit.Speed*seconds);
                if(nav.Pass((int)Math.Floor(side.X),(int)Math.Floor(side.Y)) && nav.ClearShot(before,side)
                    && battle.Units.All(other=>other.Id==unit.Id||!other.Alive||other.Position.DistanceTo(side)>=.4))
                {unit.X=side.X;unit.Y=side.Y;return true;}
            }
            return false;
        }
        return moved;
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

    public static void Cancel(Village village, Raid raid, RaidBattle battle, IReadOnlyList<Citizen> people, DateTimeOffset at)
    {
        if (battle.FinishedAt is not null) return;
        Finish(village, raid, battle, people.ToDictionary(c => c.Id), at, cancelled: true);
        foreach (var unit in battle.Units.Where(u => u.Alive))
        { unit.ExitedAt = at; unit.State = "Dispersed"; unit.TargetId = null; unit.TargetKind = null; }
        foreach (var fighter in battle.Fighters)
        { fighter.Active = false; fighter.TargetUnitId = null; }
        battle.Revision++;
    }

    private static void Finish(Village village,Raid raid,RaidBattle battle,IReadOnlyDictionary<Guid,Citizen> people,DateTimeOffset at, bool cancelled = false)
    {
        battle.Phase=cancelled?"Cancelled":"Finished";battle.FinishedAt=at;
        raid.Killed=battle.Units.Count(u=>u.Health<=0);
        raid.Defenders=battle.Fighters.Count(f=>f.DamageDealt>0);
        raid.Injured=battle.Fighters.Count(f=>f.Wounded);
        raid.DamagedBuildings=battle.Damage.Count(d=>d.Amount>0);
        raid.ResolvedAt=at;
        raid.Outcome=cancelled?RaidOutcome.Cancelled:battle.DamageDone<50&&raid.Stolen.Count==0?RaidOutcome.Repelled:RaidOutcome.Lost;
        var contributors=battle.Fighters.Where(f=>f.DamageDealt>0&&!f.RewardsGranted).OrderBy(f=>f.CitizenId).ToList();
        var total=contributors.Sum(f=>f.DamageDealt);
        var remaining=raid.Killed*RaidRules.RenownPerKill;
        for(var i=0;i<contributors.Count;i++)
        {
            var f=contributors[i];if(!people.TryGetValue(f.CitizenId,out var c))continue;
            var reward=i==contributors.Count-1?remaining:(int)Math.Floor(raid.Killed*RaidRules.RenownPerKill*f.DamageDealt/Math.Max(1,total));
            remaining-=reward;c.Renown+=reward;f.RenownEarned=reward;
            if (!cancelled || raid.Killed > 0) SkillRules.AddExperience(c,Skill.Guarding,CombatRules.FightExperience);
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
        if (!cancelled) village.AddMorale(raid.Outcome==RaidOutcome.Repelled?RaidRules.RepelledMorale:RaidRules.LostMorale,at);
        battle.Event(cancelled?"cancelled":"finished",raid.Outcome.ToString(),at);
        var old=village.Raids.Where(r=>!r.IsPending).OrderByDescending(r=>r.ResolvedAt).Skip(RaidRules.KeptRaids).Select(r=>r.Id).ToHashSet();
        village.Raids.RemoveAll(r=>old.Contains(r.Id));
        RaidRules.UpdateNextRaidAt(village);
    }
}
