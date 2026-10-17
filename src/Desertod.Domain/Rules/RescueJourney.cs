using Desertod.Domain.World;

namespace Desertod.Domain.Rules;

/// <summary>Сохраняемый маршрут спасения; часы не зависят от открытой вкладки.</summary>
public sealed class RescueJourney
{
    public Guid PatientId { get; set; }
    public bool PatientIsTsar { get; set; }
    public DateTimeOffset? WoundedAt { get; set; }
    public required string HospitalPlotId { get; set; }
    public DateTimeOffset PickupAt { get; set; }
    public DateTimeOffset ArrivesAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool Delivered { get; set; }
    public List<RescueStep> Steps { get; set; } = [];

    public TilePoint PositionAt(DateTimeOffset now)
    {
        if (Steps.Count == 0) return WorldMap.Places[WorldMap.Camp];
        var previous = Steps[0];
        foreach (var step in Steps.Skip(1))
        {
            if (now < step.At)
            {
                var share = Math.Clamp((now - previous.At).TotalSeconds / Math.Max(.001, (step.At - previous.At).TotalSeconds), 0, 1);
                return new(previous.X + (step.X - previous.X) * share, previous.Y + (step.Y - previous.Y) * share);
            }
            previous = step;
        }
        return new(previous.X, previous.Y);
    }
}

public sealed class RescueStep
{
    public double X { get; set; }
    public double Y { get; set; }
    public DateTimeOffset At { get; set; }
}
