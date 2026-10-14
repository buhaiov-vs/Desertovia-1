using Desertod.Domain.Stats;

namespace Desertod.Domain.Rules;

public readonly record struct ExperienceResult(int LevelsGained, int RenownGained);

public static class Progression
{
    /// <summary>Experience from a stat level to the next one: a share of the skill curve.</summary>
    public static int ExperienceToNext(int level)
    {
        if (level < GameRules.StatMinimum || level >= GameRules.StatMaximum)
        {
            return 0;
        }

        return (int)Math.Round(GameRules.StatCurveShare * Curve(level), MidpointRounding.AwayFromZero);
    }

    /// <summary>Experience from a skill level to the next one.</summary>
    public static int Curve(int level)
    {
        if (level == 0)
        {
            return GameRules.FirstLevelExperience;
        }

        return (int)Math.Round(GameRules.ExperienceCurveBase + GameRules.ExperienceCurvePerLevel * level);
    }

    /// <summary>
    /// Adds experience to a stat. Experience that cannot raise power (stat maxed or talent cap reached)
    /// is converted into Renown.
    /// </summary>
    public static ExperienceResult AddExperience(StatBlock stats, Stat stat, int experience)
    {
        if (experience <= 0)
        {
            return new ExperienceResult(0, 0);
        }

        var level = stats.Get(stat);
        if (level >= GameRules.StatMaximum || stats.Total >= GameRules.TalentCap)
        {
            return new ExperienceResult(0, experience);
        }

        var pool = stats.GetExperience(stat) + experience;
        var levelsGained = 0;

        while (level < GameRules.StatMaximum
               && stats.Total < GameRules.TalentCap
               && pool >= ExperienceToNext(level))
        {
            pool -= ExperienceToNext(level);
            level++;
            levelsGained++;
            stats.Set(stat, level);
        }

        var renown = 0;
        if (level >= GameRules.StatMaximum || stats.Total >= GameRules.TalentCap)
        {
            renown = pool;
            pool = 0;
        }

        stats.SetExperience(stat, pool);
        return new ExperienceResult(levelsGained, renown);
    }

    public static double ChallengeMultiplier(int level, int requirement) =>
        level <= requirement + GameRules.ChallengeThreshold ? 1.0 : GameRules.ChallengePenalty;
}
