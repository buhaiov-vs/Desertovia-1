using Desertod.Domain.Economy;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

public static class BattleActions
{
    public static readonly TimeSpan HealingCooldown = TimeSpan.FromSeconds(60);
    public const double HealingFraction = .2;

    public static bool Heal(Village village, Citizen? citizen, RaidFighter fighter, Guid requestId, DateTimeOffset now)
    {
        if(requestId==Guid.Empty)throw new GameRuleException("battle.requestId","A unique action identifier is required.");
        var receipt="|"+requestId.ToString("N")+"|";
        if(fighter.HealingReceipts?.Contains(receipt,StringComparison.Ordinal)==true)return false;
        if(!fighter.Active || fighter.Mode is "retreating" or "withdrawn" || fighter.Health<=0 || fighter.Wounded)
            throw new GameRuleException("battle.notActive","You are not actively fighting.");
        if(fighter.Health>=fighter.MaximumHealth)throw new GameRuleException("battle.fullHealth","Health is already full.");
        if(fighter.HealReadyAt>now)throw new GameRuleException("battle.healCooldown","Wait for the healing cooldown.");
        var personal=citizen is null?0:PersonalInventory.Amount(citizen,ResourceKind.Remedy);
        var carried=citizen?.Carried(ResourceKind.Remedy)??0;
        if(personal+carried+village.Stock(ResourceKind.Remedy)<1)
            throw new GameRuleException("heal.none","There is no remedy available.");
        if(personal>0)PersonalInventory.Remove(citizen!,ResourceKind.Remedy,1);
        else if(carried>0)citizen!.TakeFromLoad(ResourceKind.Remedy,1);
        else village.Take(ResourceKind.Remedy,1);
        fighter.Health=Math.Min(fighter.MaximumHealth,fighter.Health+fighter.MaximumHealth*HealingFraction);
        fighter.HealReadyAt=now+HealingCooldown;
        fighter.HealingReceipts=(fighter.HealingReceipts??"")+receipt;
        return true;
    }

    public static void Retreat(RaidBattle battle,RaidFighter fighter,RaidNavigation nav)
    {
        if(!fighter.Active || fighter.Health<=0 || fighter.Wounded)
            throw new GameRuleException("battle.notActive","You are not actively fighting.");
        if(fighter.Mode=="retreating")return;
        var from=new TilePoint(Math.Floor(fighter.X)+.5,Math.Floor(fighter.Y)+.5);
        var nearest=battle.Units.Where(u=>u.Alive).OrderBy(u=>u.Position.DistanceTo(fighter.Position)).FirstOrDefault();
        var dx=nearest is null?1:fighter.X-nearest.X;var dy=nearest is null?1:fighter.Y-nearest.Y;
        var length=Math.Max(.001,Math.Sqrt(dx*dx+dy*dy));
        var wish=new TilePoint(fighter.X+dx/length*5,fighter.Y+dy/length*5);
        var destination=nav.NearestFree(wish,p=>p.DistanceTo(fighter.Position)>=3
            && battle.Units.All(u=>!u.Alive||u.Position.DistanceTo(p)>=4)
            && nav.Next(from,p,false) is not null);
        fighter.Mode="retreating";fighter.TargetUnitId=null;
        fighter.DestinationX=destination.X;fighter.DestinationY=destination.Y;
    }

    public static void StepRetreat(RaidFighter fighter,RaidNavigation nav)
    {
        if(fighter.DestinationX is not {} x || fighter.DestinationY is not {} y)return;
        var goal=new TilePoint(x,y);
        nav.Move(fighter,goal,1);
        if(fighter.Position.DistanceTo(goal)<.25)
        {fighter.Mode="withdrawn";fighter.Active=false;fighter.TargetUnitId=null;fighter.DestinationX=null;fighter.DestinationY=null;}
    }
}
