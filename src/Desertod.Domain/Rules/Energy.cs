namespace Desertod.Domain.Rules;

public static class Energy
{
    /// <summary>Half an hour away from paid work breaks the consecutive tired-work streak.</summary>
    public static int TiredCycles(Desertod.Domain.Entities.Citizen citizen, DateTimeOffset now) =>
        now - citizen.EnergyUpdatedAt >= TimeSpan.FromMinutes(30) ? 0 : citizen.ExhaustedCycles;

    public static double Maximum(int endurance) =>
        GameRules.EnergyBase + GameRules.EnergyPerEndurance * endurance;

    public static double RegenerationPerHour(int endurance) =>
        Maximum(endurance) / GameRules.EnergyFullRegenerationHours;

    /// <summary>Energy is stored as a snapshot and regenerates continuously from that moment.</summary>
    public static double Current(double storedEnergy, DateTimeOffset storedAt, DateTimeOffset now, int endurance, double multiplier = 1)
    {
        var hours = Math.Max(0, (now - storedAt).TotalHours);
        return Regenerate(storedEnergy, hours, endurance, multiplier);
    }

    /// <param name="hours">Hours of regeneration at <paramref name="multiplier"/>.</param>
    public static double Regenerate(double storedEnergy, double hours, int endurance, double multiplier = 1) =>
        Math.Min(Maximum(endurance), storedEnergy + Math.Max(0, hours) * RegenerationPerHour(endurance) * multiplier);
}
