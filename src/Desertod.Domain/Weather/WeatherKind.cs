namespace Desertod.Domain.Weather;

/// <summary>The sky of one day or night; see <see cref="WeatherRules.WeatherOf(Entities.Village, long)"/>.</summary>
public enum WeatherKind
{
    Clear,
    Rain,

    /// <summary>Not drawn yet: <see cref="WeatherRules.WindChance"/> is 0 until wind has effects.</summary>
    Wind,

    /// <summary>Wet like rain. Not drawn yet: <see cref="WeatherRules.ThunderstormChance"/> is 0 until thunder has effects.</summary>
    Thunderstorm,
}
