using Desertod.Domain.Weather;

namespace Desertod.Api.Game;

/// <summary>The <c>Game</c> configuration section.</summary>
public sealed class GameOptions
{
    public const string Section = "Game";

    /// <summary>Real time of one daytime and one night together; see <see cref="DayCycle"/>.</summary>
    public TimeSpan DayLength { get; set; } = DayCycle.DefaultDayLength;

    /// <summary>How long a new village is safe from beasts; see <see cref="Domain.Combat.CombatRules.PeacefulTime"/>.</summary>
    public TimeSpan PeacefulTime { get; set; } = TimeSpan.FromDays(1);

    /// <summary>Development and local deployments: the client shows helpers such as the day-night toggle.</summary>
    public bool DevTools { get; set; }
}
