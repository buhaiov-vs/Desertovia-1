using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;

namespace Desertod.Domain.Buildings;

public static class ConstructionRules
{
    /// <summary>
    /// The Tsar or a Governor places a blueprint with its top-left corner on tile (<paramref name="x"/>, <paramref name="y"/>).
    /// It costs nothing: citizens bring every piece of it, and first fell any trees growing there.
    /// </summary>
    public static ConstructionSite Place(Village village, BuildingKind kind, int x, int y, DateTimeOffset now)
    {
        var definition = BuildingCatalog.Get(kind);
        if (!definition.Buildable)
        {
            throw new GameRuleException("building.notAvailable", $"{definition.Name} cannot be built yet.", Building(kind));
        }

        if (!TechnologyRules.Knows(village, definition.Technology))
        {
            throw new GameRuleException("building.needsTechnology",
                $"{definition.Name} needs {TechnologyCatalog.Get(definition.Technology!).Name}. Citizens must discover it first.",
                new Dictionary<string, object> { ["building"] = kind.ToString(), ["technology"] = definition.Technology! });
        }

        var missing = definition.Requires.Where(required => !village.Has(required)).ToList();
        if (missing.Count > 0)
        {
            throw new GameRuleException("building.requires",
                $"{definition.Name} needs: {string.Join(", ", missing.Select(m => BuildingCatalog.Get(m).Name))}.",
                new Dictionary<string, object> { ["building"] = kind.ToString(), ["requires"] = string.Join(",", missing) });
        }

        var footprint = WorldMap.Footprint(x, y);
        if (kind == BuildingKind.RopeBridge)
        {
            if (!Wilderness.Spans(village, footprint))
            {
                throw new GameRuleException("building.bridgeNeedsRavine", "A rope bridge must span the ravine from rim to rim.",
                    new Dictionary<string, object> { ["x"] = x, ["y"] = y });
            }
        }
        else if (!WorldMap.BuildArea.Contains(footprint) || WorldMap.ReservedAreas.Any(reserved => reserved.Overlaps(footprint)))
        {
            throw new GameRuleException("building.cannotBuildHere", "Nothing can be built here.",
                new Dictionary<string, object> { ["x"] = x, ["y"] = y });
        }

        if (!village.IsExplored(footprint))
        {
            throw new GameRuleException("building.unexplored", "Nobody has seen this land yet. Send someone to explore it first.");
        }

        if (village.AreaTaken(footprint))
        {
            throw new GameRuleException("building.plotTaken", "Something is already built or planned here.");
        }

        if (village.Sites.Count >= BuildingCatalog.MaximumOpenSites)
        {
            throw new GameRuleException("building.tooManySites",
                $"At most {BuildingCatalog.MaximumOpenSites} buildings can be under construction at once.",
                new Dictionary<string, object> { ["maximum"] = BuildingCatalog.MaximumOpenSites });
        }

        var site = new ConstructionSite
        {
            Kind = kind,
            PlotId = WorldMap.PlotAt(x, y),
            PlacedAt = now,
            Scale = Math.Pow(BuildingCatalog.RepeatCostGrowth, village.CountOf(kind)),
            TreesToFell = Forest.Hold(village, footprint, now),
        };
        village.Sites.Add(site);
        return site;
    }

    /// <summary>Only untouched blueprints can be withdrawn; anything already carried there stays the citizens' work.</summary>
    public static void Cancel(Village village, string plotId)
    {
        var site = FindSite(village, plotId);
        if (site.HasWork)
        {
            throw new GameRuleException("building.cannotCancel", "Citizens have already worked on this site.");
        }

        village.Sites.Remove(site);
    }

    public static ConstructionSite FindSite(Village village, string plotId) =>
        village.Sites.SingleOrDefault(s => s.PlotId == plotId)
        ?? throw new GameRuleException("site.notFound", "There is no construction site here.",
            new Dictionary<string, object> { ["plotId"] = plotId });

    /// <summary>Checks that a citizen can start a job now. Throws a rule error explaining why not.</summary>
    public static JobDefinition CheckJob(Village village, Citizen citizen, string plotId, int stage, string jobId, int recentCitizens)
    {
        var site = FindSite(village, plotId);
        var job = BuildingCatalog.FindJob(jobId)
                  ?? throw new GameRuleException("site.unknownJob", $"Unknown job \"{jobId}\".", new Dictionary<string, object> { ["jobId"] = jobId });
        var stages = site.Definition.Stages;

        if (stage < site.Stage || stage >= stages.Count)
        {
            throw new GameRuleException("site.stageDone", "This part of the building is already finished.");
        }

        if (jobId == BuildingCatalog.LendAHand)
        {
            if (stage != site.Stage || !site.JobsDone(stage)
                || site.HandCount(stage) >= ConstructionSite.CrewNeeded(stages[stage], recentCitizens))
            {
                throw new GameRuleException("site.handNotNeeded", "No extra hands are needed here right now.");
            }

            if (site.HasHand(stage, citizen.Id))
            {
                throw new GameRuleException("site.alreadyHelped", "You already worked on this stage. Someone else must lend a hand.");
            }

            return job;
        }

        if (site.Jobs(stage).All(j => j.JobId != jobId))
        {
            throw new GameRuleException("site.unknownJob", $"Unknown job \"{jobId}\".", new Dictionary<string, object> { ["jobId"] = jobId });
        }

        if (site.Remaining(stage, jobId) == 0)
        {
            throw new GameRuleException("site.jobDone", "This job is already done.");
        }

        if (job.Kind == JobKind.Haul)
        {
            var resource = job.Resource!.Value;
            if (citizen.Carried(resource) + village.Stock(resource) < 1)
            {
                throw new GameRuleException("site.noMaterial", $"There is no {Catalog.ResourceNames.Of(resource)} to carry. Gather some first.",
                    new Dictionary<string, object> { ["resource"] = resource.ToString() });
            }

            return job;
        }

        if (stage != site.Stage)
        {
            throw new GameRuleException("site.stageNotReady", $"Finish the {stages[site.Stage].Name.ToLowerInvariant()} first.",
                new Dictionary<string, object> { ["stage"] = stages[site.Stage].Id });
        }

        if (site.LaborRoom(stage, jobId) == 0)
        {
            throw new GameRuleException("site.waitingMaterial", "Waiting for material: bring it to the site first.");
        }

        return job;
    }

    /// <summary>
    /// Moves finished stages forward. A stage is finished when its jobs are done and enough different citizens
    /// worked on it. Returns the building kind when the last stage is finished.
    /// </summary>
    public static BuildingKind? Advance(Village village, ConstructionSite site, int recentCitizens, DateTimeOffset now)
    {
        var stages = site.Definition.Stages;
        while (!site.IsFinished
               && site.JobsDone(site.Stage)
               && site.HandCount(site.Stage) >= ConstructionSite.CrewNeeded(stages[site.Stage], recentCitizens))
        {
            site.Stage++;
        }

        if (!site.IsFinished)
        {
            return null;
        }

        village.Sites.Remove(site);
        if (site.Definition.Storage is { } storage)
        {
            village.Storages.Add(StorageBuilding.Create(storage, site.PlotId, now));
        }
        else
        {
            village.Structures.Add(new Structure { Kind = site.Kind, PlotId = site.PlotId, BuiltAt = now });
            FarmRules.EnsureBeds(village);
        }

        return site.Kind;
    }

    private static Dictionary<string, object> Building(BuildingKind kind) => new() { ["building"] = kind.ToString() };
}
