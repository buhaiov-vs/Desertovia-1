namespace Desertod.Domain.Weather;

/// <summary>A Synoptic's forecast for one day or night, and the weather it brought.</summary>
public sealed class WeatherForecast
{
    /// <summary>Period (a daytime or a night) the forecast is for; see <see cref="DayCycle.PeriodOf"/>.</summary>
    public long Day { get; set; }

    public Guid CitizenId { get; set; }

    /// <summary>What the Synoptic announced.</summary>
    public bool SaysRain { get; set; }

    /// <summary>Rolled when the forecast was made; nobody learns it before the day comes.</summary>
    public bool Rain { get; set; }

    public DateTimeOffset MadeAt { get; set; }
}
