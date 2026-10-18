namespace Desertod.Domain.Entities;

/// <summary>An explicitly submitted report; opening or cancelling the form never creates one.</summary>
public sealed class PlayerReport
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? VillageId { get; set; }
    public required string Kind { get; set; }
    public required string Text { get; set; }
    public required string Fingerprint { get; set; }
    public required string Payload { get; set; }
    public byte[]? Screenshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string DeliveryState { get; set; } = "Queued";
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? AttemptAt { get; set; }
    public int Attempts { get; set; }
}
