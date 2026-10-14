namespace Desertod.Domain.Rules;

/// <summary>
/// Tuning parameters. Values mirror the table in docs/game-design.md section 10.
/// </summary>
public static class GameRules
{
    /// <summary>Everyone starts wild: every stat at 0, grown only by working.</summary>
    public const int StatMinimum = 0;
    public const int StatMaximum = 10;
    public const int TalentCap = 30;

    /// <summary>
    /// Work time of gathering, farming, hunting rabbits and hauling. Walking to the place and to storage is added on top.
    /// Tasks are a few minutes long so a day of play is many short visits rather than one long session.
    /// </summary>
    public const int GatherSeconds = 180;

    /// <summary>Work time of crafting, building, research, project and camp work.</summary>
    public const int CraftSeconds = 300;

    /// <summary>A guard stays on duty well past a task of ordinary work.</summary>
    public const int GuardShiftSeconds = 1200;

    /// <summary>The reference task length: hunting experience follows the hunt's time relative to it.</summary>
    public static readonly TimeSpan CycleLength = TimeSpan.FromSeconds(GatherSeconds);
    public static readonly TimeSpan CraftLength = TimeSpan.FromSeconds(CraftSeconds);
    public static readonly TimeSpan GraceWindow = TimeSpan.FromSeconds(60);

    /// <summary>Citizens active within this window count towards the crew a construction stage needs.</summary>
    public static readonly TimeSpan RecentActivityWindow = TimeSpan.FromHours(2);

    /// <summary>
    /// A citizen whose village page is closed and who has not acted for this long is away: not counted in votes, and
    /// hidden from the map while idle. They still live in the village and are back as soon as they return.
    /// </summary>
    public static readonly TimeSpan PresenceWindow = TimeSpan.FromMinutes(5);

    public const int BaseExperiencePerCycle = 10;
    public const double SecondaryExperienceShare = 0.3;

    /// <summary>
    /// Skill curve: the first level is quick, then every level costs about a day of energy (36 tasks × 10),
    /// so a citizen gains about one profession level a day.
    /// </summary>
    public const int FirstLevelExperience = 50;
    public const double ExperienceCurveBase = 270;
    public const double ExperienceCurvePerLevel = 20;

    /// <summary>Stats level on this share of the skill curve: about two stat points a day.</summary>
    public const double StatCurveShare = 0.6;

    /// <summary>Skills level on the same curve as stats, with their own maximum and no talent cap.</summary>
    public const int SkillMaximum = 10;

    /// <summary>Skill levels above what a task needs make it yield more and take less time, up to this many levels.</summary>
    public const int SkillBonusLevels = 5;
    public const double SkillYieldPerLevel = 0.05;
    public const double SkillWorkTimePerLevel = 0.04;

    /// <summary>A citizen eats one food every this many finished cycles; a missed meal is tried again after every cycle.</summary>
    public const int CyclesPerMeal = 5;
    public const double HungryExperienceMultiplier = 0.5;
    public const double HungryRegenerationMultiplier = 0.5;
    public const double UnderTreesRegeneration = 0.8;

    public const int ChallengeThreshold = 3;
    public const double ChallengePenalty = 0.25;

    /// <summary>
    /// About 36 full-experience tasks a day for everyone: Endurance and beds change the daily budget only a little.
    /// </summary>
    public const double EnergyBase = 36;
    public const double EnergyPerEndurance = 1;
    public const double EnergyFullRegenerationHours = 24;
    public const double EnergyCostPerCycle = 1;

    /// <summary>Playtest: everyone has full energy in this window, then regeneration resumes from the stored snapshot.</summary>
    public static readonly DateTimeOffset FreeEnergyFrom = new(2026, 10, 14, 0, 0, 0, TimeSpan.FromHours(-4));
    public static readonly DateTimeOffset FreeEnergyUntil = FreeEnergyFrom.AddDays(1);

    public static bool IsFreeEnergy(DateTimeOffset at) => at >= FreeEnergyFrom && at < FreeEnergyUntil;

    /// <summary>A vigor drink gives this much energy, at most once per cooldown, so the energy pool still limits play.</summary>
    public const double VigorDrinkEnergy = 8;
    public static readonly TimeSpan VigorDrinkCooldown = TimeSpan.FromHours(2);

    public const double TiredExperienceMultiplier = 0;
    public const double TiredYieldMultiplier = 0.25;
    public const double OverflowExperienceMultiplier = 0.5;

    public const double StatFactorBase = 0.4;
    public const double StatFactorPerPoint = 0.12;
    public const double PrimaryStatWeight = 0.7;

    public const double StreakPerRenewal = 0.01;
    public const double StreakMaximumBonus = 0.20;

    public const double MoraleMinimum = 0.8;
    public const double MoraleMaximum = 1.2;
    public const double MoralePerPerformYield = 0.002;

    /// <summary>Share of the gap between morale and 1.0 that closes every minute.</summary>
    public const double MoraleDriftPerMinute = 0.02;
}
