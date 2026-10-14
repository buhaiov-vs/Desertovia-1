using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.Skills;

namespace Desertod.Domain.Rules;

public static class HospitalRules
{
    public static bool IsAdmitted(Citizen citizen, DateTimeOffset now) => citizen.Recovery?.Until > now;
    public static int Level(Village village) => village.Structures.Where(s => s.Kind == BuildingKind.Hospital)
        .Select(s => Math.Clamp(s.Level, 1, 3)).DefaultIfEmpty(0).Max();
    public static int SkillLevel(Citizen citizen) => Math.Min(5, citizen.SkillLevel(Skill.Recovery));

    public static void RequireReady(Citizen citizen, DateTimeOffset now)
    {
        if (citizen.Recovery is { Available: false })
            throw new GameRuleException("hospital.unavailable", "Hospital status is temporarily unavailable. Try again shortly.");
        if (IsAdmitted(citizen, now) || citizen.RecoveryPausedAt is not null)
            throw new GameRuleException("hospital.admitted", "You are recovering after a duel. Wait for discharge or open treatment.");
    }

    /// <summary>Preserve the exact cycle, ingredients and carried goods. No re-start or second debit on resume.</summary>
    public static void Apply(Citizen citizen, ExternalRecovery state, DateTimeOffset now)
    {
        var previousUntil = citizen.Recovery?.Until;
        citizen.Recovery = state;
        if (!state.Available || IsAdmitted(citizen, now))
        {
            if (citizen.RecoveryPausedAt is null)
            {
                var admission = state.Available ? state.AdmittedAt ?? now : now;
                citizen.RecoveryPausedAt = admission < now ? admission : now;
                // Work completed before this admission may be banked after discharge, never restarted.
            }
        }
        else if (citizen.RecoveryPausedAt is { } paused)
        {
            var discharged = previousUntil is { } end && end < now ? end : now;
            var duration = discharged > paused ? discharged - paused : TimeSpan.Zero;
            if (!citizen.CycleSettled && citizen.CycleEndsAt > paused)
            {
                citizen.CycleStartedAt += duration;
                citizen.CycleEndsAt += duration;
            }
            citizen.RecoveryPausedAt = null;
        }
    }
}
