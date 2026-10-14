namespace Desertod.Domain.Buildings;

/// <summary>A blueprint the Tsar or a Governor placed. Citizens build it stage by stage; nothing happens on its own.</summary>
public sealed class ConstructionSite
{
    public BuildingKind Kind { get; set; }

    /// <summary>Unique within a village; identifies the site.</summary>
    public required string PlotId { get; set; }

    public DateTimeOffset PlacedAt { get; set; }

    /// <summary>Material multiplier for repeats of the same building.</summary>
    public double Scale { get; set; } = 1;

    /// <summary>Index of the stage being built. Equal to the stage count when the building is finished.</summary>
    public int Stage { get; set; }

    /// <summary>Trees that stood on the plot when it was planned; the first stage gets a job to fell them.</summary>
    public int TreesToFell { get; set; }

    public List<JobProgress> Progress { get; set; } = [];

    /// <summary>Citizens who worked on each stage; a stage needs a crew of different people.</summary>
    public List<SiteHand> Hands { get; set; } = [];

    public BuildingDefinition Definition => BuildingCatalog.Get(Kind);

    public bool IsFinished => Stage >= Definition.Stages.Count;

    public bool HasWork => Progress.Any(p => p.Done > 0) || Hands.Count > 0;

    /// <summary>Jobs of the stage on this plot: the building's own, after felling the trees that grow here.</summary>
    public IReadOnlyList<StageJob> Jobs(int stage)
    {
        var jobs = Definition.Stages[stage].Jobs;
        return stage == 0 && TreesToFell > 0
            ? [new StageJob(BuildingCatalog.FellTrees, TreesToFell * BuildingCatalog.FellWorkPerTree), .. jobs]
            : jobs;
    }

    public int Required(int stage, string jobId)
    {
        var job = Jobs(stage).SingleOrDefault(j => j.JobId == jobId);
        if (job is null)
        {
            return 0;
        }

        return BuildingCatalog.FindJob(jobId)?.Kind == JobKind.Haul ? (int)Math.Ceiling(job.Amount * Scale) : job.Amount;
    }

    public int Done(int stage, string jobId) =>
        Progress.SingleOrDefault(p => p.Stage == stage && p.JobId == jobId)?.Done ?? 0;

    public int Remaining(int stage, string jobId) => Math.Max(0, Required(stage, jobId) - Done(stage, jobId));

    public void Record(int stage, string jobId, int amount, Guid citizenId)
    {
        if (amount > 0)
        {
            var progress = Progress.SingleOrDefault(p => p.Stage == stage && p.JobId == jobId);
            if (progress is null)
            {
                progress = new JobProgress { Stage = stage, JobId = jobId };
                Progress.Add(progress);
            }

            progress.Done += amount;
        }

        AddHand(stage, citizenId);
    }

    public void AddHand(int stage, Guid citizenId)
    {
        if (!Hands.Any(h => h.Stage == stage && h.CitizenId == citizenId))
        {
            Hands.Add(new SiteHand { Stage = stage, CitizenId = citizenId });
        }
    }

    public int HandCount(int stage) => Hands.Count(h => h.Stage == stage);

    public bool HasHand(int stage, Guid citizenId) => Hands.Any(h => h.Stage == stage && h.CitizenId == citizenId);

    /// <summary>Share of the stage's material that has arrived, 0 to 1. Stages without material count as 1.</summary>
    public double MaterialShare(int stage)
    {
        var hauls = Jobs(stage)
            .Where(j => BuildingCatalog.FindJob(j.JobId)?.Kind == JobKind.Haul)
            .ToList();
        if (hauls.Count == 0)
        {
            return 1;
        }

        var required = hauls.Sum(j => Required(stage, j.JobId));
        var done = hauls.Sum(j => Math.Min(Done(stage, j.JobId), Required(stage, j.JobId)));
        return required <= 0 ? 1 : (double)done / required;
    }

    /// <summary>Labor may not run ahead of the material: half the stones delivered allows half the stone-laying.</summary>
    public int LaborRoom(int stage, string jobId) =>
        BuildingCatalog.FindJob(jobId)?.NeedsMaterial == true
            ? Math.Max(0, (int)Math.Floor(Required(stage, jobId) * MaterialShare(stage) + 1e-9) - Done(stage, jobId))
            : Remaining(stage, jobId);

    public bool JobsDone(int stage) =>
        Jobs(stage).All(j => Remaining(stage, j.JobId) == 0);

    public static int CrewNeeded(StageDefinition stage, int recentCitizens) =>
        Math.Max(1, Math.Min(stage.Crew, recentCitizens));

    /// <summary>Overall progress, 0 to 1, for the banner and drawing the building as it rises.</summary>
    public double Fraction()
    {
        var stages = Definition.Stages;
        var total = 0.0;
        for (var index = 0; index < stages.Count; index++)
        {
            if (index < Stage)
            {
                total += 1;
                continue;
            }

            var jobs = Jobs(index);
            var required = jobs.Sum(j => Required(index, j.JobId));
            var done = jobs.Sum(j => Math.Min(Done(index, j.JobId), Required(index, j.JobId)));
            total += required <= 0 ? 0 : (double)done / required * 0.99;
        }

        return stages.Count == 0 ? 0 : total / stages.Count;
    }
}

public sealed class JobProgress
{
    public int Stage { get; set; }
    public required string JobId { get; set; }
    public int Done { get; set; }
}

public sealed class SiteHand
{
    public int Stage { get; set; }
    public Guid CitizenId { get; set; }
}

/// <summary>A finished building that is not a storage building (shelters and workplaces).</summary>
public sealed class Structure
{
    public BuildingKind Kind { get; set; }
    public required string PlotId { get; set; }
    public DateTimeOffset BuiltAt { get; set; }

    /// <summary>Harm done by raiders, 0 to <see cref="Combat.CombatRules.MaximumDamage"/>; repaired by citizens.</summary>
    public int Damage { get; set; }
}
