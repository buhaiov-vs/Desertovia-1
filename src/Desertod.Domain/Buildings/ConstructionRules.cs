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
    public static ConstructionSite Place(Village village, BuildingKind kind, int x, int y, DateTimeOffset now, Guid? privateOwner = null)
    {
        ValidatePlacement(village, kind, x, y, privateOwner);
        var footprint = WorldMap.Footprint(x, y);
        var site = new ConstructionSite
        {
            Kind = kind,
            OwnerAccountId = privateOwner,
            PlotId = WorldMap.PlotAt(x, y),
            PlacedAt = now,
            Scale = Math.Pow(BuildingCatalog.RepeatCostGrowth, village.CountOf(kind,privateOwner)),
            TreesToFell = Forest.Hold(village, footprint, now),
        };
        village.Sites.Add(site);
        return site;
    }

    /// <summary>Pure validation: pending applications do not occupy land, fell trees or consume a construction slot.</summary>
    public static void ValidatePlacement(Village village, BuildingKind kind, int x, int y, Guid? privateOwner = null, bool checkCapacity = true)
    {
        var definition = BuildingCatalog.Get(kind);
        if (kind == BuildingKind.Castle && village.CountOf(kind) > 0)
            throw new GameRuleException("castle.unique", "Only one royal castle can be built.");
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
        BankAccess.Require(village, WorldMap.Yard(WorldMap.PlotAt(x, y)));
        if (privateOwner is not null && (kind is BuildingKind.Castle or BuildingKind.Hospital or BuildingKind.RopeBridge || KingdomRules.Area(village).Contains(footprint)))
            throw new GameRuleException("building.privateKind", "This building requires the Tsar's approval.");
        if (kind == BuildingKind.RopeBridge)
        {
            if (!Wilderness.Spans(village, footprint))
            {
                throw new GameRuleException("building.bridgeNeedsRavine", "A rope bridge must span the ravine from rim to rim.",
                    new Dictionary<string, object> { ["x"] = x, ["y"] = y });
            }
        }
        else if (!(privateOwner is null ? KingdomRules.Area(village).Contains(footprint) : WorldMap.Land.Contains(footprint)) || WorldMap.ReservedAreas.Any(reserved => reserved.Overlaps(footprint)))
        {
            throw new GameRuleException("building.cannotBuildHere", "Nothing can be built here.",
                new Dictionary<string, object> { ["x"] = x, ["y"] = y });
        }

        if (kind != BuildingKind.RopeBridge && !WorldMap.BuildArea.Contains(footprint) && !KingdomRules.ClearGround(village,footprint))
            throw new GameRuleException("building.clearGround", "Choose dry, cleared land away from the river and ravine.");
        if (!village.IsExplored(footprint))
        {
            throw new GameRuleException("building.unexplored", "Nobody has seen this land yet. Send someone to explore it first.");
        }

        if (village.AreaTaken(footprint))
        {
            throw new GameRuleException("building.plotTaken", "Something is already built or planned here.");
        }

        var maximumSites = privateOwner is null ? BuildingCatalog.MaximumOpenSites : 1;
        if (checkCapacity && village.Sites.Count(s=>s.OwnerAccountId==privateOwner) >= maximumSites)
        {
            throw new GameRuleException("building.tooManySites",
                $"At most {maximumSites} buildings can be under construction at once.",
                new Dictionary<string, object> { ["maximum"] = maximumSites });
        }

    }

    public static ConstructionSite PlacePrivate(Village village, Guid owner, BuildingKind kind, int x, int y, DateTimeOffset now) =>
        Place(village, kind, x, y, now, owner);

    public static ConstructionSite UpgradeHospital(Village village, string plotId, DateTimeOffset now)
    {
        var hospital = village.Structures.SingleOrDefault(s => s.Kind == BuildingKind.Hospital && s.PlotId == plotId)
            ?? throw new GameRuleException("hospital.notBuilt", "Build the hospital first.");
        if (hospital.Level >= 3 || village.Sites.Any(s => s.PlotId == plotId))
            throw new GameRuleException("hospital.upgradeUnavailable", "The hospital is already upgrading or at maximum level.");
        if (village.Sites.Count(s=>s.OwnerAccountId==null) >= BuildingCatalog.MaximumOpenSites)
            throw new GameRuleException("building.tooManySites", "Finish another construction first.");
        var site = new ConstructionSite { Kind = BuildingKind.Hospital, PlotId = plotId,
            TargetLevel = hospital.Level + 1, Scale = Math.Pow(2, hospital.Level), PlacedAt = now };
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

        if(site.Operation is not null)
        {
            var structure=village.Structures.SingleOrDefault(b=>b.PlotId==site.PlotId);if(structure is not null)structure.Decommissioning=false;
            var storage=village.Storages.SingleOrDefault(b=>b.PlotId==site.PlotId);if(storage is not null)storage.Decommissioning=false;
            var order=village.CivicOrders.SingleOrDefault(o=>o.Id==site.OrderId);if(order is not null)order.Status="cancelled";
        }
        var application = village.CivicOrders.SingleOrDefault(o => o.Id == site.OrderId);
        if (application is not null) application.Status = "cancelled";
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
        if (site.OwnerAccountId is {} owner && citizen.AccountId != owner)
            throw new GameRuleException("building.private", "Only the owner can work on this private site.");
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
                || site.HandCount(stage) >= site.RequiredCrew(stage, recentCitizens))
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
            if ((site.OwnerAccountId is null ? citizen.Carried(resource) + village.Stock(resource) : Desertod.Domain.Economy.PersonalInventory.Amount(citizen, resource)) < 1)
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
    public static BuildingKind? Advance(Village village, ConstructionSite site, int recentCitizens, DateTimeOffset now, Citizen? worker = null)
    {
        var stages = site.Definition.Stages;
        while (!site.IsFinished
               && site.JobsDone(site.Stage)
               && site.HandCount(site.Stage) >= site.RequiredCrew(site.Stage, recentCitizens))
        {
            site.Stage++;
        }

        if (!site.IsFinished)
        {
            return null;
        }

        if(BuildingOperations.Finish(village,site,now,worker))return site.Kind;
        var application = village.CivicOrders.SingleOrDefault(o => o.Id == site.OrderId);
        if (application is not null) application.Status = "completed";
        village.Sites.Remove(site);
        if (site.Definition.Storage is { } storage)
        {
            var built=StorageBuilding.Create(storage, site.PlotId, now);
            built.OwnerAccountId = site.OwnerAccountId;
            BuildingOperations.AddInvestment(site,built.Investment);
            village.Storages.Add(built);
        }
        else
        {
            if (site.TargetLevel > 1)
            {
                var building = village.Structures.Single(s => s.PlotId == site.PlotId && s.Kind == BuildingKind.Hospital);
                building.Level = site.TargetLevel;
            }
            else
            {
                var built=new Structure { Kind = site.Kind, PlotId = site.PlotId, BuiltAt = now, OwnerAccountId = site.OwnerAccountId };
                built.OwnerAccountId = site.OwnerAccountId;
                BuildingOperations.AddInvestment(site,built.Investment);
                village.Structures.Add(built);
            }
            FarmRules.EnsureBeds(village);
        }

        return site.Kind;
    }

    private static Dictionary<string, object> Building(BuildingKind kind) => new() { ["building"] = kind.ToString() };
}
