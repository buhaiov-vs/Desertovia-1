using Desertod.Domain.Catalog;

namespace Desertod.Domain.Projects;

public enum ProjectStatus
{
    Active,
    Completed,
}

public enum ProjectPhase
{
    Survey,
    Construction,
    Completed,
}

public sealed class VillageProject
{
    public const string SurveyKey = "Survey";
    public const string BuildKey = "Build";

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VillageId { get; set; }
    public required string DefinitionId { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int RulesVersion { get; set; }
    public int RewardedStages { get; set; }
    public List<string> BridgeHands { get; set; } = [];
    public static readonly int[] BridgeEnds = [40, 90, 160, 210, 218];
    public int BridgeStage => Array.FindIndex(BridgeEnds, end => BuildProgress < end) is var i && i >= 0 ? i : 5;
    public bool StagedBridge => RulesVersion >= 2 && DefinitionId == ProjectCatalog.TsarBridge;
    public void RegisterHand(Guid id) { var key=$"{BridgeStage}:{id}"; if (!BridgeHands.Contains(key)) BridgeHands.Add(key); }
    public bool HasCrew(Guid candidate, int active) => BridgeHands.Where(h => h.StartsWith($"{BridgeStage}:"))
        .Append($"{BridgeStage}:{candidate}").Distinct().Count() >= Math.Min(3, Math.Max(1,active));
    public int SurveyProgress { get; set; }
    public int BuildProgress { get; set; }
    public List<ProjectMaterial> Materials { get; set; } = [];
    public List<ProjectContribution> Contributions { get; set; } = [];

    public ProjectDefinition Definition =>
        StagedBridge ? ProjectCatalog.Find(DefinitionId)! with { SurveyCycles = 12, BuildCycles = 218, Materials =
            [new(ResourceKind.Wood,240),new(ResourceKind.Stone,160),new(ResourceKind.Reeds,80),new(ResourceKind.Planks,60)] } :
        ProjectCatalog.Find(DefinitionId) ?? throw new InvalidOperationException($"Unknown project '{DefinitionId}'.");

    public static VillageProject Start(Guid villageId, ProjectDefinition definition, DateTimeOffset now)
    {
        var project = new VillageProject { VillageId = villageId, DefinitionId = definition.Id, StartedAt = now, RulesVersion = 2 };
        foreach (var material in project.Definition.Materials)
        {
            project.Materials.Add(new ProjectMaterial { VillageProjectId = project.Id, Kind = material.Resource });
        }

        return project;
    }

    public bool SurveyDone => SurveyProgress >= Definition.SurveyCycles;

    public bool MaterialsDone => Definition.Materials.All(m => Delivered(m.Resource) >= m.Amount);

    public bool BuildDone => BuildProgress >= Definition.BuildCycles;

    public bool IsComplete => SurveyDone && MaterialsDone && BuildDone;

    public ProjectPhase Phase => Status == ProjectStatus.Completed ? ProjectPhase.Completed
        : SurveyDone ? ProjectPhase.Construction
        : ProjectPhase.Survey;

    public int Delivered(ResourceKind kind) => Materials.SingleOrDefault(m => m.Kind == kind)?.Delivered ?? 0;

    public double MaterialsFraction => Definition.Materials.Count == 0
        ? 1
        : Definition.Materials.Average(m => Math.Min(1, (double)Delivered(m.Resource) / m.Amount));

    /// <summary>Construction cannot run ahead of the delivered materials.</summary>
    public int BuildLimit => Math.Min(StagedBridge && BridgeStage < BridgeEnds.Length ? BridgeEnds[BridgeStage] : int.MaxValue, MaterialsDone
        ? Definition.BuildCycles
        : (int)Math.Floor(MaterialsFraction * Definition.BuildCycles));

    public double Progress =>
        ProjectRules.SurveyWeight * (Definition.SurveyCycles == 0 ? 1 : Math.Min(1, (double)SurveyProgress / Definition.SurveyCycles))
        + ProjectRules.MaterialsWeight * MaterialsFraction
        + ProjectRules.BuildWeight * Math.Min(1, (double)BuildProgress / Definition.BuildCycles);

    public int Required(string key) => key switch
    {
        SurveyKey => Definition.SurveyCycles,
        BuildKey => Definition.BuildCycles,
        _ => Definition.Materials.SingleOrDefault(m => m.Resource.ToString() == key)?.Amount ?? 0,
    };

    public int Done(string key) => key switch
    {
        SurveyKey => SurveyProgress,
        BuildKey => BuildProgress,
        _ => Enum.TryParse<ResourceKind>(key, out var kind) ? Delivered(kind) : 0,
    };

    public int Remaining(string key) => Math.Max(0, Required(key) - Done(key));

    public int Contributed(Guid citizenId, string key) =>
        Contributions.SingleOrDefault(c => c.CitizenId == citizenId && c.Key == key)?.Amount ?? 0;

    public int ContributionRoom(Guid citizenId, string key, int activeCitizens) =>
        Math.Max(0, ProjectRules.ContributionCap(Required(key), activeCitizens) - Contributed(citizenId, key));

    public void Record(Guid citizenId, string key, int amount)
    {
        switch (key)
        {
            case SurveyKey: SurveyProgress += amount; break;
            case BuildKey: if (StagedBridge && amount > 0) RegisterHand(citizenId); BuildProgress += amount; break;
            default:
                Materials.Single(m => m.Kind.ToString() == key).Delivered += amount;
                break;
        }

        var contribution = Contributions.SingleOrDefault(c => c.CitizenId == citizenId && c.Key == key);
        if (contribution is null)
        {
            contribution = new ProjectContribution { VillageProjectId = Id, CitizenId = citizenId, Key = key };
            Contributions.Add(contribution);
        }

        contribution.Amount += amount;
    }

    /// <summary>Share of the whole project done by a citizen: average of their share over every requirement.</summary>
    public double ShareOf(Guid citizenId)
    {
        var keys = Definition.Materials.Select(m => m.Resource.ToString()).Append(SurveyKey).Append(BuildKey).ToList();
        return keys.Average(key => Required(key) <= 0 ? 0 : (double)Contributed(citizenId, key) / Required(key));
    }
}

public sealed class ProjectMaterial
{
    public Guid VillageProjectId { get; set; }
    public ResourceKind Kind { get; set; }
    public int Delivered { get; set; }
}

public sealed class ProjectContribution
{
    public Guid VillageProjectId { get; set; }
    public Guid CitizenId { get; set; }
    public required string Key { get; set; }
    public int Amount { get; set; }
}
