using Desertod.Domain.Buildings;
using Desertod.Domain.Combat;
using Desertod.Domain.Entities;
using Desertod.Domain.Skills;

namespace Desertod.Domain.Rules;

public static class HospitalRules
{
    public static bool IsAdmitted(Citizen citizen, DateTimeOffset now) => citizen.Recovery?.Until > now;
    public static int Level(Village village) => village.Structures.Where(s => s.Kind == BuildingKind.Hospital && s.RuinedAt is null && s.Damage<100)
        .Select(s => Math.Clamp(s.Level, 1, 3)).DefaultIfEmpty(0).Max();
    public static int SkillLevel(Citizen citizen) => Math.Min(5, citizen.SkillLevel(Skill.Recovery));

    public static void RequireReady(Citizen citizen, DateTimeOffset now)
    {
        Resume(citizen, now);
        if (citizen.Recovery is { Available: false })
            throw new GameRuleException("hospital.unavailable", "Hospital status is temporarily unavailable. Try again shortly.");
        if (citizen.RecoveryPausedAt is not null && CombatRules.IsInjured(citizen,now) && !IsAdmitted(citizen,now))
            throw new GameRuleException("citizen.injured", "You are wounded and cannot work until recovery.",
                new Dictionary<string,object>{["readyAt"]=citizen.InjuredUntil!.Value});
        if (IsAdmitted(citizen, now) || citizen.RecoveryPausedAt is not null)
            throw new GameRuleException("hospital.admitted", "You are recovering after a duel. Wait for discharge or open treatment.");
    }

    /// <summary>Preserve the exact cycle, ingredients and carried goods. No re-start or second debit on resume.</summary>
    public static void Apply(Citizen citizen, ExternalRecovery state, DateTimeOffset now)
    {
        var previousUntil = citizen.Recovery?.Until;
        citizen.Recovery = state;
        if (!state.Available || IsAdmitted(citizen, now) || CombatRules.IsInjured(citizen, now) && !citizen.CycleSettled)
        {
            if (citizen.RecoveryPausedAt is null)
            {
                var admission = state.Available ? state.AdmittedAt ?? now : now;
                citizen.RecoveryPausedAt = admission < now ? admission : now;
                if(citizen.CurrentTaskId=="supervise")citizen.SupervisedProgress=int.MaxValue;
                // Work completed before this admission may be banked after discharge, never restarted.
            }
        }
        else Resume(citizen, now, previousUntil);
    }

    /// <summary>Shared pause for overlapping native wounds and external hospital stays: resume once.</summary>
    public static bool Resume(Citizen citizen, DateTimeOffset now, DateTimeOffset? previousUntil = null)
    {
        if(citizen.RecoveryPausedAt is not {} paused || citizen.Recovery?.Available==false
            || IsAdmitted(citizen,now) || CombatRules.IsInjured(citizen,now))return false;
        var ends=new[]{previousUntil,citizen.Recovery?.Until,citizen.InjuredUntil}
            .Where(at=>at is not null).Select(at=>at!.Value).ToArray();
        var discharged=ends.Length==0?now:ends.Max();
        if(discharged>now)discharged=now;
        var duration=discharged>paused?discharged-paused:TimeSpan.Zero;
        if(!citizen.CycleSettled && citizen.CycleEndsAt>paused)
        {
            citizen.CycleStartedAt+=duration;
            citizen.CycleEndsAt+=duration;
        }
        citizen.RecoveryPausedAt=null;
        return true;
    }
}
