using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;

namespace Desertod.Domain.Tests;

public class ProgressionTests
{
    private static readonly double TasksPerDay = Energy.RegenerationPerHour(0) * 24 / GameRules.EnergyCostPerCycle;

    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 174)]
    [InlineData(4, 210)]
    [InlineData(9, 270)]
    [InlineData(10, 0)]
    public void StatExperienceToNext_IsAShareOfTheSkillCurve(int level, double expected) =>
        Assert.Equal(expected, Progression.ExperienceToNext(level));

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 290)]
    [InlineData(9, 450)]
    [InlineData(10, 0)]
    public void SkillExperienceToNext_MatchesDesignTable(int level, double expected) =>
        Assert.Equal(expected, SkillRules.ExperienceToNext(level));

    [Fact]
    public void EveryProfessionLevel_TakesAboutADayOfEnergy()
    {
        Assert.Equal(36, TasksPerDay, 6);
        foreach (var level in Enumerable.Range(1, GameRules.SkillMaximum - 1))
        {
            var days = SkillRules.ExperienceToNext(level) / (TasksPerDay * GameRules.BaseExperiencePerCycle);
            Assert.InRange(days, 0.75, 1.3);
        }
    }

    [Fact]
    public void Stats_GrowAboutTwoPointsADay()
    {
        var perDay = TasksPerDay * GameRules.BaseExperiencePerCycle * (1 + GameRules.SecondaryExperienceShare);
        var total = Enumerable.Range(0, 10).Sum(Progression.ExperienceToNext);
        var pointsPerDay = perDay / (total / 10.0);

        Assert.Equal(2028, total);
        Assert.InRange(pointsPerDay, 1.8, 2.6);
    }

    [Fact]
    public void AddExperience_LevelsUpAndKeepsRemainder()
    {
        var stats = new StatBlock();

        var result = Progression.AddExperience(stats, Stat.Strength, 600);

        Assert.Equal(4, result.LevelsGained);
        Assert.Equal(4, stats.Strength);
        Assert.Equal(12, stats.StrengthExperience);
        Assert.Equal(0, result.RenownGained);
    }

    [Fact]
    public void AddExperience_AtTalentCap_BecomesRenown()
    {
        var stats = new StatBlock { Strength = 10, Dexterity = 10, Intelligence = 8, Charisma = 1, Endurance = 1 };

        var result = Progression.AddExperience(stats, Stat.Charisma, 1000);

        Assert.Equal(1, stats.Charisma);
        Assert.Equal(1000, result.RenownGained);
    }

    [Fact]
    public void AddExperience_ReachingTalentCapMidway_ConvertsLeftoverToRenown()
    {
        var stats = new StatBlock { Strength = 10, Dexterity = 10, Intelligence = 7, Charisma = 1, Endurance = 1 };

        var result = Progression.AddExperience(stats, Stat.Charisma, 1000);

        Assert.Equal(2, stats.Charisma);
        Assert.Equal(GameRules.TalentCap, stats.Total);
        Assert.Equal(826, result.RenownGained);
        Assert.Equal(0, stats.CharismaExperience);
    }

    [Theory]
    [InlineData(4, 1, 1.0)]
    [InlineData(5, 1, 0.25)]
    [InlineData(7, 4, 1.0)]
    [InlineData(8, 4, 0.25)]
    [InlineData(10, 7, 1.0)]
    public void ChallengeMultiplier_PenalizesTrivialTasks(int stat, int requirement, double expected) =>
        Assert.Equal(expected, Progression.ChallengeMultiplier(stat, requirement));
}
