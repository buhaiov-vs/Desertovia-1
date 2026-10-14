using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;

namespace Desertod.Domain.Skills;

public static class SkillRules
{
    /// <summary>Skill trained by researching any technology.</summary>
    public const Skill Research = Skill.Scholarship;

    public static int ExperienceToNext(int level) =>
        level is < 0 or >= GameRules.SkillMaximum ? 0 : Progression.Curve(level);

    /// <summary>The citizen's stats are enough for the next level of the skill.</summary>
    public static bool CanReachNext(Citizen citizen, Skill skill)
    {
        var level = citizen.SkillLevel(skill);
        return level < GameRules.SkillMaximum && SkillCatalog.Get(skill).Allows(citizen.Stats, level + 1);
    }

    /// <summary>
    /// Adds experience to a skill; returns the levels gained. A level the stats are not enough for waits:
    /// experience for it is kept, the rest is lost, and the level comes once the stats catch up (see <see cref="CatchUp"/>).
    /// Experience past the maximum level is lost.
    /// </summary>
    public static int AddExperience(Citizen citizen, Skill skill, int experience)
    {
        if (experience <= 0)
        {
            return 0;
        }

        var progress = citizen.Skills.SingleOrDefault(s => s.Kind == skill);
        if (progress is null)
        {
            progress = new SkillProgress { Kind = skill };
            citizen.Skills.Add(progress);
        }

        if (progress.Level >= GameRules.SkillMaximum)
        {
            return 0;
        }

        progress.Experience += experience;
        return LevelUp(citizen, progress);
    }

    /// <summary>Raises the skills whose next level was waiting for the stats the citizen has just gained.</summary>
    public static void CatchUp(Citizen citizen)
    {
        foreach (var progress in citizen.Skills)
        {
            LevelUp(citizen, progress);
        }
    }

    private static int LevelUp(Citizen citizen, SkillProgress progress)
    {
        var definition = SkillCatalog.Get(progress.Kind);
        var levelsGained = 0;
        while (progress.Level < GameRules.SkillMaximum
               && progress.Experience >= ExperienceToNext(progress.Level)
               && definition.Allows(citizen.Stats, progress.Level + 1))
        {
            progress.Experience -= ExperienceToNext(progress.Level);
            progress.Level++;
            levelsGained++;
        }

        progress.Experience = Math.Min(progress.Experience, ExperienceToNext(progress.Level));
        return levelsGained;
    }

    /// <summary>Skill levels above what the work needs, counted up to <see cref="GameRules.SkillBonusLevels"/>.</summary>
    public static int Mastery(int level, int required) => Math.Clamp(level - required, 0, GameRules.SkillBonusLevels);

    public static double YieldMultiplier(Citizen citizen, Skill skill, int required) =>
        1 + GameRules.SkillYieldPerLevel * Mastery(citizen.SkillLevel(skill), required);

    /// <summary>Applies to the work only; walking takes as long as ever.</summary>
    public static TimeSpan WorkTime(Citizen citizen, Skill skill, int required, TimeSpan work) =>
        work * (1 - GameRules.SkillWorkTimePerLevel * Mastery(citizen.SkillLevel(skill), required));

    /// <summary>Experience multiplier: work far below the citizen's level in its craft teaches little.</summary>
    public static double Challenge(Citizen citizen, Skill skill, int required) =>
        Progression.ChallengeMultiplier(citizen.SkillLevel(skill), required);

    public static void Check(Citizen citizen, TaskDefinition task)
    {
        if (citizen.SkillLevel(task.Skill) >= task.SkillLevel)
        {
            return;
        }

        throw new GameRuleException("task.skillRequired", $"\"{task.Name}\" needs {task.Skill} {task.SkillLevel}.",
            new Dictionary<string, object> { ["taskId"] = task.Id, ["skill"] = task.Skill.ToString(), ["level"] = task.SkillLevel });
    }
}
