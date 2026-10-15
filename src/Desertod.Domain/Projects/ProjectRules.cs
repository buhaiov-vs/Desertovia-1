using Desertod.Domain.Entities;
using Desertod.Domain.Storage;

namespace Desertod.Domain.Projects;

public sealed record ProjectCompletion(string ProjectId, string ProjectName, IReadOnlyDictionary<Guid, int> RenownByCitizen);

public static class ProjectRules
{
    public const double SurveyWeight = 0.1;
    public const double MaterialsWeight = 0.5;
    public const double BuildWeight = 0.4;

    /// <summary>No citizen may provide more than this share of one requirement...</summary>
    public const double ContributionCapShare = 0.2;

    /// <summary>...except in small villages, where the cap is relaxed to an equal split.</summary>
    public static int ContributionCap(int required, int activeCitizens) =>
        (int)Math.Ceiling(required * Math.Max(ContributionCapShare, 1.0 / Math.Max(1, activeCitizens)) - 1e-9);

    public const int SupervisionBase = 3;

    public static int SupervisionCapacity(int engineerIntelligence) => 3 + Math.Clamp((engineerIntelligence - 1) / 2, 0, 3);

    /// <summary>Marks the project complete, rewards the village and splits Renown by contribution share.</summary>
    public static ProjectCompletion Complete(
        VillageProject project,
        Village village,
        IReadOnlyDictionary<Guid, Citizen> contributors,
        DateTimeOffset now)
    {
        var reward = project.Definition.Reward;
        if (project.DefinitionId == ProjectCatalog.TsarBridge) village.BridgeOpened = true;
        project.Status = ProjectStatus.Completed;
        project.CompletedAt = now;

        village.AddMorale(reward.Morale, now);
        if (reward.Storage is { } storage && village.Storages.All(s => s.PlotId != project.DefinitionId))
        {
            village.Storages.Add(StorageBuilding.Create(storage, project.DefinitionId, now));
        }

        var shares = contributors.Keys.ToDictionary(id => id, project.ShareOf);
        var total = shares.Values.Sum();
        var renown = new Dictionary<Guid, int>();
        var exact = shares.Select(kv => new { Id = kv.Key, Value = total <= 0 ? 0 : reward.RenownPool * kv.Value / total }).ToList();
        var allocation = exact.ToDictionary(x => x.Id, x => (int)Math.Floor(x.Value));
        var remainder = total <= 0 ? 0 : reward.RenownPool - allocation.Values.Sum();
        foreach (var item in exact.OrderByDescending(x => x.Value - Math.Floor(x.Value)).ThenBy(x => x.Id).Take(remainder)) allocation[item.Id]++;
        foreach (var (id, amount) in allocation) { contributors[id].Renown += amount; renown[id] = amount; }

        return new ProjectCompletion(project.DefinitionId, project.Definition.Name, renown);
    }
}
