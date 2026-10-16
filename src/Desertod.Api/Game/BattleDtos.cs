using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;

namespace Desertod.Api.Game;

public sealed record BattleUnitDto(Guid Id,string Variant,double X,double Y,double Health,double MaximumHealth,
    double Attack,double Speed,double Range,string State,string? TargetKind,string? TargetId,
    DateTimeOffset NextAttackAt,DateTimeOffset? DiedAt,DateTimeOffset? ExitedAt);
public sealed record BattleFighterDto(Guid CitizenId,double X,double Y,double Health,double MaximumHealth,
    double DamageDealt,int Kills,int RenownEarned,bool Wounded,bool Guard,bool Active,string Mode,Guid? TargetUnitId,
    DateTimeOffset NextAttackAt,ResourceKind? Weapon,ResourceKind? Armor,int Arrows, bool IsTsar = false, Guid? AccountId = null, DateTimeOffset? HealReadyAt = null);
public sealed record BattleEventDto(int Sequence,string Kind,string Target,DateTimeOffset At);
public sealed record RaidBattleDto(Guid Id,Guid VillageId,int EngineVersion,string Phase,string Sector,string Tactic,
    string Composition,double ThreatBudget,int ReadyDefenders,int AvailableReserves,DateTimeOffset StartsAt,
    DateTimeOffset LastTickAt,DateTimeOffset EndsAt,DateTimeOffset? FinishedAt,long Revision,bool Deferred,
    IReadOnlyList<BattleUnitDto> Units,IReadOnlyList<BattleFighterDto> Fighters,IReadOnlyList<BattleEventDto> Events);

public static class BattleDtos
{
    // Detached immutable messages: a retry or subsequent simulation step cannot mutate queued positions.
    public static RaidBattleDto ToBattleDto(this RaidBattle b) => new(b.Id,b.VillageId,b.EngineVersion,b.Phase,
        b.Sector,b.Tactic,b.Composition,b.ThreatBudget,b.ReadyDefenders,b.AvailableReserves,b.StartsAt,b.LastTickAt,
        b.EndsAt,b.FinishedAt,b.Revision,b.Deferred,
        b.Units.Select(u=>new BattleUnitDto(u.Id,u.Variant,u.X,u.Y,u.Health,u.MaximumHealth,u.Attack,u.Speed,u.Range,
            u.State,u.TargetKind,u.TargetId,u.NextAttackAt,u.DiedAt,u.ExitedAt)).ToArray(),
        b.Fighters.Select(f=>new BattleFighterDto(f.CitizenId,f.X,f.Y,f.Health,f.MaximumHealth,f.DamageDealt,f.Kills,
            f.RenownEarned,f.Wounded,f.Guard,f.Active,f.Mode,f.TargetUnitId,f.NextAttackAt,f.Weapon,f.Armor,f.Arrows,f.IsTsar,f.AccountId,f.HealReadyAt)).ToArray(),
        b.Events.TakeLast(8).Select(e=>new BattleEventDto(e.Sequence,e.Kind,e.Target,e.At)).ToArray());
}
