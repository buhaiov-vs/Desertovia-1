using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Technologies;

namespace Desertod.Domain.Weather;

/// <summary>
/// The sky over the village. Every daytime and every night (a period of <see cref="DayCycle"/>) draws its own
/// <see cref="WeatherKind"/> for the whole village as it begins. The draw comes from the village id and the period, so it
/// needs no stored state and every server agrees on it, unless a Synoptic forecast the period. The Synoptic is famously
/// wrong: what he announces does not happen, the more surely the better he knows the weather.
/// </summary>
public static class WeatherRules
{
    public const string SynopticRole = "synoptic";

    /// <summary>Every period draws its own rain chance between these.</summary>
    public const double MinimumRainChance = 0.05;
    public const double MaximumRainChance = 0.10;

    public const double WindChance = 0;
    public const double ThunderstormChance = 0;

    /// <summary>Kinds the sky draws, in the order their chances are stacked; whatever is left is <see cref="WeatherKind.Clear"/>.</summary>
    private static readonly WeatherKind[] Drawn = [WeatherKind.Rain, WeatherKind.Thunderstorm, WeatherKind.Wind];

    /// <summary>Chance the weather turns out opposite to the forecast, at Weatherlore 0 and at the maximum level.</summary>
    public const double MinimumReliability = 0.6;
    public const double MaximumReliability = 0.95;

    public const double FishingYield = 1.25;

    /// <summary>Research and tavern talk: people stay inside.</summary>
    public const double IndoorYield = 1.15;

    public const double FireMorale = 0.5;

    /// <summary>Construction, hauling and deliveries in the mud.</summary>
    public const double MudYield = 0.75;

    /// <summary>Animals hide and nothing is seen far away.</summary>
    public const double HuntingYield = 0.8;

    public const double OutdoorEnergyCost = 1.5;

    /// <summary>Regeneration of those sleeping under the trees, on top of the trees' own penalty.</summary>
    public const double UnderTreesRegeneration = 0.5;

    /// <summary>An hour of rain counts as this many hours of regrowth for felled trees.</summary>
    public const double ForestRegrowth = 2;

    /// <summary>Forecasts older than this are forgotten; the sky of those days falls back to the drawn one.</summary>
    public static TimeSpan ForecastMemory { get; } = TimeSpan.FromDays(7);

    /// <summary>Rain is counted at most this far back; energy, crops and trees are full long before.</summary>
    private static readonly TimeSpan Lookback = TimeSpan.FromDays(14);

    private const string FishTask = "fish";
    private const string FireTask = "tend-fire";

    private static readonly HashSet<BuildingKind> IndoorBuildings =
    [
        BuildingKind.Library, BuildingKind.Tavern, BuildingKind.Smithy, BuildingKind.Workshop, BuildingKind.Bakery,
        BuildingKind.Mine, BuildingKind.Barracks,
    ];

    public static double RainChance(Village village, long period) =>
        MinimumRainChance + (MaximumRainChance - MinimumRainChance) * Noise(village.Id, period, 1);

    public static double Chance(Village village, long period, WeatherKind kind) => kind switch
    {
        WeatherKind.Rain => RainChance(village, period),
        WeatherKind.Wind => WindChance,
        WeatherKind.Thunderstorm => ThunderstormChance,
        _ => 0,
    };

    /// <summary>Rain and thunderstorms: what the rain effects follow.</summary>
    public static bool IsWet(WeatherKind kind) => kind is WeatherKind.Rain or WeatherKind.Thunderstorm;

    /// <summary>The weather of the period as the sky draws it when the period begins, without a forecast.</summary>
    public static WeatherKind SkyDraws(Village village, long period)
    {
        var roll = Noise(village.Id, period, 2);
        var threshold = 0.0;
        foreach (var kind in Drawn)
        {
            threshold += Chance(village, period, kind);
            if (roll < threshold)
            {
                return kind;
            }
        }

        return WeatherKind.Clear;
    }

    public static bool SkyRains(Village village, long period) => IsWet(SkyDraws(village, period));

    public static WeatherForecast? ForecastFor(Village village, long period) => village.Forecasts.FirstOrDefault(f => f.Day == period);

    /// <summary>A forecast only decides whether it is wet; a dry period keeps the rest of what the sky drew.</summary>
    public static WeatherKind WeatherOf(Village village, long period)
    {
        var drawn = SkyDraws(village, period);
        return ForecastFor(village, period) switch
        {
            null => drawn,
            { Rain: true } => IsWet(drawn) ? drawn : WeatherKind.Rain,
            _ => IsWet(drawn) ? WeatherKind.Clear : drawn,
        };
    }

    public static WeatherKind WeatherOf(Village village, DateTimeOffset time) => WeatherOf(village, DayCycle.PeriodOf(time));

    public static bool IsRaining(Village village, long period) => IsWet(WeatherOf(village, period));

    public static bool IsRaining(Village village, DateTimeOffset time) => IsRaining(village, DayCycle.PeriodOf(time));

    /// <summary>Hours of rain between the two moments.</summary>
    public static double RainHours(Village village, DateTimeOffset from, DateTimeOffset to) =>
        Segments(village, from, to).Where(s => s.Rain).Sum(s => (s.End - s.Start).TotalHours);

    /// <summary>Hours a farm bed was wet between the two moments: watered until <paramref name="wateredUntil"/>, or rained on.</summary>
    public static double WetHours(Village village, DateTimeOffset from, DateTimeOffset to, DateTimeOffset? wateredUntil)
    {
        var hours = 0.0;
        foreach (var (start, end, rain) in Segments(village, from, to))
        {
            if (rain)
            {
                hours += (end - start).TotalHours;
            }
            else if (wateredUntil is { } until && until > start)
            {
                hours += ((until < end ? until : end) - start).TotalHours;
            }
        }

        return hours;
    }

    public static double Reliability(int weatherlore) =>
        MinimumReliability + (MaximumReliability - MinimumReliability) * Math.Clamp(weatherlore, 0, GameRules.SkillMaximum) / GameRules.SkillMaximum;

    /// <summary>The village knows the weather lore and the citizen has the Weatherlore of a Synoptic.</summary>
    public static bool IsSynoptic(Village village, Citizen citizen)
    {
        var role = GameCatalog.FindRole(SynopticRole)!;
        return TechnologyRules.Knows(village, role.Technology) && role.IsQualified(citizen);
    }

    /// <summary>
    /// A Synoptic who has just slept (idle, past the grace window of the last task) announces the weather of the next
    /// day or night. Only the first forecast of a period counts. The weather turns out opposite to the announcement with
    /// <see cref="Reliability"/>; otherwise the sky does what it would have done anyway.
    /// </summary>
    public static WeatherForecast Forecast(Village village, Citizen citizen, bool saysRain, Random random, DateTimeOffset now)
    {
        var role = GameCatalog.FindRole(SynopticRole)!;
        if (!TechnologyRules.Knows(village, role.Technology))
        {
            throw new GameRuleException("forecast.needsTechnology",
                $"Forecasting needs {TechnologyCatalog.Get(role.Technology!).Name}. The village must discover it first.",
                new Dictionary<string, object> { ["technology"] = role.Technology! });
        }

        if (!role.IsQualified(citizen))
        {
            throw new GameRuleException("task.roleRequired", $"Only a {role.Name} ({role.Skill} {role.SkillLevel}) can do this.",
                new Dictionary<string, object> { ["roleId"] = role.Id, ["skill"] = role.Skill.ToString()!, ["level"] = role.SkillLevel });
        }

        if (citizen.Status != CitizenStatus.Active)
        {
            throw new GameRuleException("citizen.dormant", "Dormant citizens cannot work.");
        }

        if (citizen.WorkState(now) != WorkState.Idle)
        {
            throw new GameRuleException("forecast.notRested", "A Synoptic reads the sky right after sleeping. Rest first.");
        }

        var period = DayCycle.PeriodOf(now) + 1;
        if (ForecastFor(village, period) is not null)
        {
            throw new GameRuleException("forecast.alreadyMade", "The weather of the next day or night has already been forecast.");
        }

        var reversed = random.NextDouble() < Reliability(citizen.SkillLevel(Skill.Weatherlore));
        var forecast = new WeatherForecast
        {
            Day = period,
            CitizenId = citizen.Id,
            SaysRain = saysRain,
            Rain = reversed ? !saysRain : SkyRains(village, period),
            MadeAt = now,
        };
        village.Forecasts.RemoveAll(f => DayCycle.PeriodStart(f.Day + 1) < now - ForecastMemory);
        village.Forecasts.Add(forecast);
        return forecast;
    }

    /// <summary>Indoor work keeps dry: no extra energy in the rain.</summary>
    public static bool IsIndoor(TaskDefinition task) => task.RequiredBuilding is { } building && IndoorBuildings.Contains(building);

    /// <summary>Energy a cycle costs when it starts at <paramref name="at"/>.</summary>
    public static double EnergyCost(Village village, bool outdoor, DateTimeOffset at) =>
        GameRules.EnergyCostPerCycle * (outdoor && IsRaining(village, at) ? OutdoorEnergyCost : 1);

    /// <summary>Rain's effect on the yield of a task done at <paramref name="at"/>.</summary>
    public static double YieldMultiplier(Village village, TaskDefinition task, DateTimeOffset at)
    {
        if (!IsRaining(village, at))
        {
            return 1;
        }

        return task switch
        {
            { Id: FishTask } => FishingYield,
            { Id: FireTask } => FireMorale,
            { Hunt: not null } or { Effect: TaskEffect.Roam } => HuntingYield,
            { Effect: TaskEffect.Build or TaskEffect.Deliver or TaskEffect.Survey } => MudYield,
            { RequiredBuilding: BuildingKind.Library or BuildingKind.Tavern } => IndoorYield,
            _ => 1,
        };
    }

    /// <summary>Researching a technology is indoor work.</summary>
    public static double ResearchMultiplier(Village village, DateTimeOffset at) => IsRaining(village, at) ? IndoorYield : 1;

    /// <summary>Hauling to and working on construction sites.</summary>
    public static double JobMultiplier(Village village, DateTimeOffset at) => IsRaining(village, at) ? MudYield : 1;

    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End, bool Rain)> Segments(Village village, DateTimeOffset from, DateTimeOffset to)
    {
        if (to - from > Lookback)
        {
            from = to - Lookback;
        }

        for (var period = DayCycle.PeriodOf(from); from < to; period++)
        {
            var next = DayCycle.PeriodStart(period + 1);
            var end = next < to ? next : to;
            yield return (from, end, IsRaining(village, period));
            from = end;
        }
    }

    /// <summary>A number in [0, 1) that depends only on the arguments.</summary>
    private static double Noise(Guid seed, long period, ulong salt)
    {
        Span<byte> bytes = stackalloc byte[16];
        seed.TryWriteBytes(bytes);
        var state = Mix(BitConverter.ToUInt64(bytes) ^ Mix(BitConverter.ToUInt64(bytes[8..])));
        state = Mix(state ^ unchecked((ulong)period * 0x9E3779B97F4A7C15UL ^ salt * 0xD1B54A32D192ED03UL));
        return (state >> 11) * (1.0 / (1UL << 53));
    }

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
