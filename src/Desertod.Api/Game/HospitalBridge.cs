using Desertod.Api.Auth;

namespace Desertod.Api.Game;

public sealed record HospitalPatientRequest(long Id, int HospitalLevel, int SkillLevel);
public sealed record HospitalTier(string Code, string Title, int MinHours, int MaxHours, int Price, string Blocked);
public sealed record MedicalEffect(string Kind, double Until);
public sealed record HospitalPatient(string Id, string? EpisodeId, double? AdmittedAt, double? Until,
    double CheckedAt, int HospitalPercent, int SkillPercent, IReadOnlyList<HospitalTier> Tiers, MedicalEffect? Effect = null);
public sealed record HospitalStatusReply(IReadOnlyList<HospitalPatient> Patients, int Balance);
public sealed record HospitalTreatmentReply(string State, string? Error, double? Until, int? Price, int? CutSeconds);

public sealed record Relationship(string Id, string? SpouseId, string? Name, double? Since);
public sealed record RelationshipReply(IReadOnlyList<Relationship> Relationships);

public interface IHospitalBridge
{
    bool Enabled { get; }
    Task<RelationshipReply> RelationshipsAsync(long actor, IReadOnlyList<long> ids, CancellationToken ct) =>
        Task.FromResult(new RelationshipReply([]));
    Task<HospitalStatusReply> StatusAsync(long actor, IReadOnlyList<HospitalPatientRequest> patients, CancellationToken ct);
    Task<HospitalTreatmentReply> HealAsync(long actor, long patient, HospitalHealRequest request, CancellationToken ct);
}

/// <summary>The same fixed loopback, server-authenticated bridge as Telegram login; no browser-supplied payer IDs.</summary>
public sealed class HospitalBridge(IServiceScopeFactory scopes, Microsoft.Extensions.Options.IOptions<Desertod.Api.Auth.TelegramOptions> options,
    Microsoft.Extensions.Options.IOptions<GameOptions> gameOptions) : IHospitalBridge
{
    public bool Enabled => gameOptions.Value.HospitalEnabled && Uri.TryCreate(options.Value.BrowserBridgeUrl, UriKind.Absolute, out var uri)
        && uri.IsLoopback && uri.Scheme == "http" && options.Value.IsConfigured;
    public async Task<RelationshipReply> RelationshipsAsync(long actor, IReadOnlyList<long> ids, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TelegramBrowserBridge>().CallAsync<RelationshipReply>(
            "relationships", new { actor, ids }, ct);
    }
    public async Task<HospitalStatusReply> StatusAsync(long actor, IReadOnlyList<HospitalPatientRequest> patients, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TelegramBrowserBridge>().CallAsync<HospitalStatusReply>(
            "hospital", new { operation = "status", actor, patients }, ct);
    }
    public async Task<HospitalTreatmentReply> HealAsync(long actor, long patient, HospitalHealRequest request, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TelegramBrowserBridge>().CallAsync<HospitalTreatmentReply>(
            "hospital", new { operation = "heal", actor, patient, request.Code, request.RequestId, request.ExpectedPrice, request.EpisodeId }, ct);
    }
}
