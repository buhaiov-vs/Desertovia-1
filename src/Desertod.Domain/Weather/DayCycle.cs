namespace Desertod.Domain.Weather;

/// <summary>
/// Days and nights over every village. A day of <see cref="DayLength"/> real time is cut into two periods of
/// <see cref="PeriodLength"/>: daytime first, then the night. Periods count from the Unix epoch, so every server agrees on them.
/// Each period brings its own weather; see <see cref="WeatherRules"/>.
/// </summary>
public static class DayCycle
{
    public static TimeSpan DefaultDayLength { get; } = TimeSpan.FromHours(4);

    /// <summary>Shorter days would change the weather faster than a cycle of work.</summary>
    public static TimeSpan MinimumDayLength { get; } = TimeSpan.FromMinutes(2);

    public static TimeSpan DayLength { get; private set; } = DefaultDayLength;

    /// <summary>A daytime or a night.</summary>
    public static TimeSpan PeriodLength => DayLength / 2;

    /// <summary>
    /// Sets the length of a day at startup, before any village is read. Changing it later shifts every period,
    /// so forecasts already made would land on other periods.
    /// </summary>
    public static void Configure(TimeSpan dayLength)
    {
        if (dayLength < MinimumDayLength)
        {
            throw new ArgumentOutOfRangeException(nameof(dayLength), dayLength, $"A day must last at least {MinimumDayLength}.");
        }

        DayLength = dayLength;
    }

    public static long PeriodOf(DateTimeOffset time) => (time - DateTimeOffset.UnixEpoch).Ticks / PeriodLength.Ticks;

    public static DateTimeOffset PeriodStart(long period) => DateTimeOffset.UnixEpoch + PeriodLength * period;

    public static bool IsNight(long period) => (period & 1) == 1;

    public static bool IsNight(DateTimeOffset time) => IsNight(PeriodOf(time));
}
