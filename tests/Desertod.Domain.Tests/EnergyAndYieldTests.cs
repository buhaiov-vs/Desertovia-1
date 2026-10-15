using Desertod.Domain.Rules;

namespace Desertod.Domain.Tests;

public class EnergyAndYieldTests
{
    [Theory]
    [InlineData(1, 37)]
    [InlineData(10, 46)]
    public void EnergyMaximum_DependsOnEndurance(int endurance, double expected) =>
        Assert.Equal(expected, Energy.Maximum(endurance));

    [Fact]
    public void Energy_RegeneratesFromEmptyToFullInTwelveRealHours()
    {
        var start = DateTimeOffset.UnixEpoch;

        Assert.Equal(18.5, Energy.Current(0, start, start.AddHours(6), 1), 6);
        Assert.Equal(37, Energy.Current(0, start, start.AddHours(12), 1));
    }

    [Fact]
    public void Tired_streak_warning_matches_the_half_hour_rest_reset()
    {
        var village = TestWorld.NewVillage();
        var citizen = TestWorld.NewCitizen(village);
        citizen.ExhaustedCycles = 2;
        citizen.EnergyUpdatedAt = TestWorld.Now;
        Assert.Equal(2, Energy.TiredCycles(citizen, TestWorld.Now.AddMinutes(29)));
        Assert.Equal(0, Energy.TiredCycles(citizen, TestWorld.Now.AddMinutes(30)));
        Assert.Equal(2, citizen.ExhaustedCycles); // Quoting a warning never changes persisted progression.
    }

    [Theory]
    [InlineData(1, 0.52)]
    [InlineData(5, 1.0)]
    [InlineData(10, 1.6)]
    public void StatFactor_HasAboutThreeTimesSpread(double effectiveStat, double expected) =>
        Assert.Equal(expected, YieldMath.StatFactor(effectiveStat), 6);

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(5, 1.05)]
    [InlineData(100, 1.2)]
    public void StreakFactor_IsCapped(int renewals, double expected) =>
        Assert.Equal(expected, YieldMath.StreakFactor(renewals), 6);
}
