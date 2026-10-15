using Desertod.Domain.Combat;

namespace Desertod.Domain.Director;

public sealed class AlmightyState
{
    public Guid VillageId { get; set; }
    public string Mode { get; set; } = "Propose";
    public long Version { get; set; }
    public string Intent { get; set; } = "Наблюдать за городом и предлагать царю полезные события.";
    public DateTimeOffset NextReviewAt { get; set; }
    public DateTimeOffset? LastReviewAt { get; set; }
    public DateTimeOffset? RestUntil { get; set; }
    public Guid? Lease { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? LastNoticeAt { get; set; }
    public string LastSignal { get; set; } = "";
    public string LastError { get; set; } = "";
    public DateTimeOffset BudgetDay { get; set; }
    public int CallsToday { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string LastSnapshot { get; set; } = "{}";
}

public sealed class AlmightyProposal
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public Guid ReviewId { get; set; }
    public string Option { get; set; } = "none";
    public string Reason { get; set; } = "";
    public string Intent { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string NoticeState { get; set; } = "Queued";
    [System.Text.Json.Serialization.JsonIgnore]
    public string Snapshot { get; set; } = "{}";
    public string Result { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Title => Descriptor("title");
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Effect => Descriptor("effect");
    private string Descriptor(string property)
    {
        try {
            using var doc = System.Text.Json.JsonDocument.Parse(Snapshot);
            foreach(var option in doc.RootElement.GetProperty("options").EnumerateArray())
                if(option.GetProperty("id").GetString()==Option) return option.GetProperty(property).GetString() ?? Option;
        } catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException) { }
        return Option;
    }
}

public sealed class AlmightyObservation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public DateTimeOffset At { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string Snapshot { get; set; } = "{}";
    public string Option { get; set; } = "none";
    public string Reason { get; set; } = "";
}

public sealed record AlmightyFacts(int Population, int Online, int ReadyGuards, int Patients, int PendingRaids,
    int BuildingSites, int DamagedBuildings, int RecentLosses, bool Peaceful, bool Resting);
public sealed record AlmightyOption(string Id, string Title, string Effect, EnemyKind? Enemy = null, string? Sector = null, string? Composition = null);

public static class AlmightyPolicy
{
    public static readonly TimeSpan ReviewInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ImportantInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ProposalLifetime = TimeSpan.FromMinutes(30);
    public const int DailyModelLimit = 96;
    public static IReadOnlyList<AlmightyOption> Options(AlmightyFacts f, IReadOnlyList<RaidApproach>? approaches = null)
    {
        var result = new List<AlmightyOption> {
            new("none", "Наблюдать", "Не менять мир и не беспокоить царя без причины"),
            new("economy_report", "Разобрать нужды города", "Только рекомендации по известным запасам/работам; деньги и цены не меняются") };
        if (f.Patients > 0 || f.RecentLosses > 0)
            result.Add(new("rest", "Передышка на 2 реальных часа", "Не назначать новые автоматические нападения; объявленные волны сохраняются"));
        if (f.BuildingSites > 0 || f.DamagedBuildings > 0)
            result.Add(new("work_notice", "Объявить о нужной работе", "Одна короткая реплика о существующей стройке/ремонте; без ускорения, выдачи ресурсов и принуждения"));
        if (!f.Peaceful && !f.Resting && f.PendingRaids == 0 && f.Online >= 3 && f.ReadyGuards >= 1 && f.RecentLosses == 0 && f.Patients * 3 < Math.Max(1, f.Population))
        {
            foreach (var enemy in EnemyCatalog.Enemies.Where(e => e.Undead && !e.DungeonOnly))
            {
                var prefix = enemy.Kind.ToString().ToLowerInvariant() + "s";
                result.Add(new(prefix + "_small", "Малая волна: " + enemy.Name,
                    $"Предупреждение3 реальные минуты; живой бой с отдельными врагами. Состав ограничен готовностью деревни при объявлении; ранение{enemy.InjuryMinutes} реальных минут. Обычные награды и ремонтируемый ущерб.", enemy.Kind));
                foreach (var approach in approaches ?? [])
                {
                    foreach(var profile in new[]{"mixed","swarm","elite"})
                    {
                        var id=prefix+"_"+approach.Sector+(profile=="mixed"?"":"_"+profile);
                        var label=profile=="swarm"?"много слабых":profile=="elite"?"немного сильных":"смешанный состав";
                        result.Add(new(id,enemy.Name+": "+approach.Sector+", "+label,
                            "Одна сторона, отдельные враги, проход через вход или пролом. Общий бюджет по готовности при объявлении, без скрытого усиления после подготовки.",
                            enemy.Kind,approach.Sector,profile));
                    }
                }
            }
        }
        return result;
    }
    public static bool IsMode(string mode) => mode is "Off" or "Observe" or "Propose";
}
