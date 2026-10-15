namespace Desertod.Domain.Entities;

/// <summary>Only the authenticated bot bridge can supply this external duel status.</summary>
public sealed class ExternalRecovery
{
    public string Cause { get; set; } = "duel";
    public string? EpisodeId { get; set; }
    public DateTimeOffset? AdmittedAt { get; set; }
    public DateTimeOffset? Until { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public bool Available { get; set; }
    public int HospitalPercent { get; set; }
    public int SkillPercent { get; set; }
    public string? EffectKind { get; set; }
    public DateTimeOffset? EffectUntil { get; set; }
}
