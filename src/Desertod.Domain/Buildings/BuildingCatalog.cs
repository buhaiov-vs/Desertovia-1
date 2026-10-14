using Desertod.Domain.Catalog;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;

namespace Desertod.Domain.Buildings;

public enum BuildingKind
{
    Heap,
    LeanTo,
    WoodPile,
    StonePile,
    DryingRack,
    Hut,
    Woodyard,
    StoneYard,
    Granary,
    Storehouse,
    Treasury,
    Farm,
    Mine,
    Smithy,
    Workshop,
    Library,
    Tavern,
    Stage,
    Market,
    Barracks,
    Bakery,
    RopeBridge,
}

public enum JobKind
{
    /// <summary>Carries <see cref="JobDefinition.Resource"/> from storage (or the citizen's hands) to the site.</summary>
    Haul,

    /// <summary>Work on the site. Limited by how much of the stage's material has arrived.</summary>
    Labor,
}

/// <param name="PerCycle">Units moved or work done per cycle by a citizen with every stat at 0.</param>
/// <param name="NeedsMaterial">Labor that works the stage's material and so cannot run ahead of its delivery.</param>
public sealed record JobDefinition(
    string Id,
    string Name,
    JobKind Kind,
    ResourceKind? Resource,
    int PerCycle,
    Stat Primary,
    Stat Secondary,
    Skill Skill,
    bool NeedsMaterial = false);

public sealed record StageJob(string JobId, int Amount);

/// <param name="Crew">Different citizens who must work on the stage before it is finished.</param>
public sealed record StageDefinition(string Id, string Name, int Crew, IReadOnlyList<StageJob> Jobs);

/// <param name="Stages">Empty for buildings that cannot be built yet (later ages).</param>
/// <param name="Shelter">Citizens who sleep here.</param>
/// <param name="ShelterQuality">Energy regeneration multiplier for the citizens sleeping here.</param>
/// <param name="Technology">The village must have discovered it before the building can be planned.</param>
public sealed record BuildingDefinition(
    BuildingKind Kind,
    string Name,
    string Description,
    IReadOnlyList<BuildingKind> Requires,
    IReadOnlyList<StageDefinition> Stages,
    StorageKind? Storage = null,
    int Shelter = 0,
    double ShelterQuality = 1,
    string? Technology = null)
{
    public bool Buildable => Stages.Count > 0;
}

public static class BuildingCatalog
{
    public const string LendAHand = "lend-a-hand";
    public const string FellTrees = "fell-trees";
    public const int MaximumOpenSites = 3;

    /// <summary>Work to fell one tree standing on a plot; every unit of it brings a share of the tree's wood.</summary>
    public const int FellWorkPerTree = 3;

    /// <summary>Each repeat of a building needs this much more material than the previous one.</summary>
    public const double RepeatCostGrowth = 1.15;

    public static readonly TimeSpan HaulBaseTime = GameRules.CycleLength;

    private static JobDefinition Haul(string id, string name, ResourceKind resource, int perCycle, Stat primary, Stat secondary) =>
        new(id, name, JobKind.Haul, resource, perCycle, primary, secondary, Skill.Hauling);

    private static JobDefinition Labor(
        string id, string name, Stat primary, Stat secondary, bool needsMaterial = true, Skill skill = Skill.Building) =>
        new(id, name, JobKind.Labor, null, 1, primary, secondary, skill, needsMaterial);

    public static IReadOnlyList<JobDefinition> Jobs { get; } =
    [
        Haul("bring-sticks", "Bring sticks", ResourceKind.Wood, 4, Stat.Endurance, Stat.Strength),
        Haul("bring-logs", "Bring logs", ResourceKind.Wood, 4, Stat.Strength, Stat.Endurance),
        Haul("bring-stones", "Bring stones", ResourceKind.Stone, 3, Stat.Strength, Stat.Endurance),
        Haul("bring-reeds", "Bring reeds", ResourceKind.Reeds, 5, Stat.Endurance, Stat.Dexterity),
        Haul("bring-hides", "Bring hides", ResourceKind.Leather, 1, Stat.Endurance, Stat.Dexterity),
        Haul("bring-planks", "Bring planks", ResourceKind.Planks, 3, Stat.Endurance, Stat.Strength),
        Labor(FellTrees, "Fell the trees", Stat.Strength, Stat.Endurance, needsMaterial: false, skill: Skill.Woodcutting),
        Labor("clear-ground", "Clear the ground", Stat.Strength, Stat.Endurance, needsMaterial: false),
        Labor("mark-outline", "Mark the outline", Stat.Intelligence, Stat.Dexterity, needsMaterial: false),
        Labor("lean-poles", "Lean the poles", Stat.Strength, Stat.Dexterity),
        Labor("lay-cover", "Lay the cover", Stat.Dexterity, Stat.Endurance),
        Labor("stack-pile", "Stack the pile", Stat.Strength, Stat.Endurance),
        Labor("lay-ring", "Lay the stone ring", Stat.Strength, Stat.Dexterity),
        Labor("tie-rack", "Tie the rack", Stat.Dexterity, Stat.Intelligence),
        Labor("lay-stones", "Lay stones", Stat.Strength, Stat.Endurance),
        Labor("hew-beams", "Hew beams", Stat.Strength, Stat.Dexterity),
        Labor("raise-posts", "Raise posts", Stat.Strength, Stat.Endurance),
        Labor("tie-frame", "Tie the frame", Stat.Dexterity, Stat.Strength),
        Labor("mix-clay", "Mix clay", Stat.Endurance, Stat.Strength),
        Labor("weave-walls", "Weave and daub walls", Stat.Dexterity, Stat.Endurance),
        Labor("bundle-thatch", "Bundle thatch", Stat.Dexterity, Stat.Intelligence),
        Labor("thatch-roof", "Thatch the roof", Stat.Dexterity, Stat.Endurance),
        Labor("hang-door", "Hang the door", Stat.Dexterity, Stat.Charisma),
        Labor("plough-field", "Plough the field", Stat.Endurance, Stat.Strength, needsMaterial: false, skill: Skill.Farming),
        Labor("dig-shaft", "Dig the shaft", Stat.Strength, Stat.Endurance, needsMaterial: false, skill: Skill.Mining),
        Labor("build-forge", "Build the forge", Stat.Strength, Stat.Dexterity),
        Labor("build-oven", "Build the oven", Stat.Dexterity, Stat.Strength),
        Labor("lay-floor", "Lay the floor", Stat.Dexterity, Stat.Strength),
        Labor("stretch-ropes", "Stretch the ropes", Stat.Dexterity, Stat.Strength),
        Labor("lay-deck", "Lay the deck", Stat.Strength, Stat.Dexterity),
        Labor(LendAHand, "Lend a hand", Stat.Endurance, Stat.Charisma, needsMaterial: false),
    ];

    private static StageJob Job(string id, int amount) => new(id, amount);

    private static StageDefinition Stage(string id, string name, int crew, params StageJob[] jobs) => new(id, name, crew, jobs);

    public static IReadOnlyList<BuildingDefinition> Buildings { get; } =
    [
        new(BuildingKind.Heap, "Heap", "A fenced patch of ground for any goods. Small and messy.", [],
            [Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("bring-sticks", 4))],
            Storage: StorageKind.Heap),
        new(BuildingKind.LeanTo, "Lean-to", "Poles and reeds against the wind. Two people sleep better here than under a tree.", [],
            [
                Stage("frame", "Frame", 2, Job("clear-ground", 1), Job("bring-sticks", 8), Job("lean-poles", 2)),
                Stage("cover", "Cover", 2, Job("bring-reeds", 10), Job("lay-cover", 2)),
            ],
            Shelter: 2, ShelterQuality: 0.9),
        new(BuildingKind.WoodPile, "Wood pile", "Stacked wood stays dry and does not crowd the heap.", [],
            [Stage("pile", "Pile", 2, Job("clear-ground", 1), Job("bring-sticks", 6), Job("stack-pile", 2))],
            Storage: StorageKind.WoodPile),
        new(BuildingKind.StonePile, "Stone pile", "A ring of stones that holds more stones.", [],
            [Stage("ring", "Ring", 2, Job("clear-ground", 1), Job("bring-stones", 6), Job("lay-ring", 2))],
            Storage: StorageKind.StonePile),
        new(BuildingKind.DryingRack, "Drying rack", "Meat and berries dry here instead of rotting in the heap.", [],
            [
                Stage("frame", "Frame", 2, Job("bring-sticks", 8), Job("tie-rack", 2)),
                Stage("cover", "Cover", 1, Job("bring-reeds", 6), Job("lay-cover", 1)),
            ],
            Storage: StorageKind.DryingRack),
        new(BuildingKind.Hut, "Hut", "The first real house: stone base, timber frame, clay walls, thatched roof. Four people sleep well here.",
            [BuildingKind.LeanTo],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 3), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 24), Job("lay-stones", 4)),
                Stage("frame", "Frame", 4, Job("bring-logs", 20), Job("hew-beams", 4), Job("raise-posts", 3), Job("tie-frame", 2)),
                Stage("walls", "Walls", 3, Job("bring-sticks", 12), Job("mix-clay", 3), Job("weave-walls", 4)),
                Stage("roof", "Roof", 3, Job("bring-reeds", 16), Job("bundle-thatch", 3), Job("thatch-roof", 4)),
                Stage("finish", "Finish", 1, Job("bring-hides", 2), Job("hang-door", 1)),
            ],
            Shelter: 4),
        new(BuildingKind.Woodyard, "Woodyard", "A roofed yard for wood, planks and reeds.",
            [BuildingKind.Hut, BuildingKind.WoodPile],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 12), Job("lay-stones", 3)),
                Stage("frame", "Frame", 4, Job("bring-logs", 30), Job("hew-beams", 4), Job("raise-posts", 4), Job("tie-frame", 3)),
                Stage("roof", "Roof", 3, Job("bring-reeds", 20), Job("bundle-thatch", 3), Job("thatch-roof", 4)),
            ],
            Storage: StorageKind.Woodyard),
        new(BuildingKind.StoneYard, "Stone yard", "Walled yard for stone, ore and coal.",
            [BuildingKind.Hut, BuildingKind.StonePile],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("walls", "Walls", 3, Job("bring-stones", 36), Job("lay-stones", 6)),
                Stage("frame", "Frame", 3, Job("bring-logs", 16), Job("raise-posts", 3), Job("tie-frame", 2)),
                Stage("roof", "Roof", 3, Job("bring-reeds", 16), Job("thatch-roof", 3)),
            ],
            Storage: StorageKind.StoneYard),
        new(BuildingKind.Granary, "Granary", "Raised floor and thick walls keep food from mice and rot.",
            [BuildingKind.Hut, BuildingKind.DryingRack],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 20), Job("lay-stones", 4)),
                Stage("frame", "Frame", 4, Job("bring-logs", 24), Job("hew-beams", 4), Job("raise-posts", 3), Job("tie-frame", 2)),
                Stage("walls", "Walls", 3, Job("bring-sticks", 12), Job("mix-clay", 3), Job("weave-walls", 4)),
                Stage("roof", "Roof", 3, Job("bring-reeds", 20), Job("bundle-thatch", 3), Job("thatch-roof", 4)),
            ],
            Storage: StorageKind.Granary),
        new(BuildingKind.Storehouse, "Storehouse", "Shelves for iron, leather and finer goods.", [],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 20), Job("lay-stones", 4)),
                Stage("frame", "Frame", 4, Job("bring-logs", 20), Job("raise-posts", 3), Job("tie-frame", 2)),
                Stage("floor", "Floor", 3, Job("bring-planks", 12), Job("lay-floor", 3)),
                Stage("roof", "Roof", 3, Job("bring-reeds", 16), Job("thatch-roof", 3)),
            ],
            Storage: StorageKind.Storehouse, Technology: TechnologyCatalog.Bookkeeping),
        new(BuildingKind.Treasury, "Treasury", "A strong room for gold. Needs a later age.", [BuildingKind.Smithy], [],
            Storage: StorageKind.Treasury),
        new(BuildingKind.Farm, "Farm", "Fields to tend.", [],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 3), Job("mark-outline", 1)),
                Stage("field", "Field", 3, Job("plough-field", 4)),
                Stage("fence", "Fence", 2, Job("bring-sticks", 16), Job("tie-frame", 2)),
            ],
            Technology: TechnologyCatalog.Agriculture),
        new(BuildingKind.Bakery, "Bakery", "A clay oven under a roof. Grain goes in, bread comes out.", [BuildingKind.Farm],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 16), Job("lay-stones", 3)),
                Stage("oven", "Oven", 3, Job("bring-stones", 12), Job("mix-clay", 2), Job("build-oven", 3)),
                Stage("roof", "Roof", 3, Job("bring-logs", 12), Job("raise-posts", 2), Job("bring-reeds", 12), Job("thatch-roof", 3)),
            ],
            Technology: TechnologyCatalog.Baking),
        new(BuildingKind.Mine, "Mine", "A shaft for coal, ore and gold.", [],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("shaft", "Shaft", 3, Job("dig-shaft", 6)),
                Stage("frame", "Frame", 3, Job("bring-logs", 20), Job("raise-posts", 3), Job("tie-frame", 2)),
            ],
            Technology: TechnologyCatalog.Mining),
        new(BuildingKind.Smithy, "Smithy", "Forge for iron and chains.", [],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 24), Job("lay-stones", 4)),
                Stage("forge", "Forge", 3, Job("bring-stones", 16), Job("build-forge", 4)),
                Stage("roof", "Roof", 3, Job("bring-logs", 12), Job("raise-posts", 2), Job("bring-reeds", 12), Job("thatch-roof", 3)),
            ],
            Technology: TechnologyCatalog.Smelting),
        new(BuildingKind.Workshop, "Workshop", "Saws planks and builds mechanisms.", [],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 16), Job("lay-stones", 3)),
                Stage("frame", "Frame", 4, Job("bring-logs", 24), Job("hew-beams", 4), Job("raise-posts", 3), Job("tie-frame", 2)),
                Stage("roof", "Roof", 3, Job("bring-reeds", 16), Job("bundle-thatch", 2), Job("thatch-roof", 3)),
            ],
            Technology: TechnologyCatalog.Carpentry),
        new(BuildingKind.Library, "Library", "Chronicles, research and remedies.", [],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 20), Job("lay-stones", 4)),
                Stage("frame", "Frame", 4, Job("bring-logs", 20), Job("hew-beams", 3), Job("raise-posts", 3), Job("tie-frame", 2)),
                Stage("floor", "Floor", 3, Job("bring-planks", 16), Job("lay-floor", 3)),
                Stage("roof", "Roof", 3, Job("bring-reeds", 16), Job("bundle-thatch", 3), Job("thatch-roof", 4)),
            ],
            Technology: TechnologyCatalog.Writing),
        new(BuildingKind.Tavern, "Tavern", "Talk and rest.", [],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 16), Job("lay-stones", 3)),
                Stage("frame", "Frame", 4, Job("bring-logs", 24), Job("hew-beams", 3), Job("raise-posts", 3), Job("tie-frame", 2)),
                Stage("walls", "Walls", 3, Job("bring-sticks", 12), Job("mix-clay", 3), Job("weave-walls", 4)),
                Stage("roof", "Roof", 3, Job("bring-reeds", 16), Job("thatch-roof", 3)),
            ],
            Technology: TechnologyCatalog.Brewing),
        new(BuildingKind.Stage, "Stage", "Music lifts the spirit.", [],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("floor", "Floor", 3, Job("bring-planks", 12), Job("lay-floor", 3)),
                Stage("frame", "Frame", 2, Job("bring-logs", 12), Job("raise-posts", 2)),
            ],
            Technology: TechnologyCatalog.Music),
        new(BuildingKind.Market, "Market", "Trade with other villages. Needs a later age.", [], []),
        new(BuildingKind.Barracks, "Barracks", "Guards patrol and captains command from here. Makes the camp's defense stronger.", [],
            [
                Stage("ground", "Ground", 2, Job("clear-ground", 2), Job("mark-outline", 1)),
                Stage("foundation", "Foundation", 3, Job("bring-stones", 24), Job("lay-stones", 4)),
                Stage("frame", "Frame", 4, Job("bring-logs", 24), Job("hew-beams", 3), Job("raise-posts", 3), Job("tie-frame", 2)),
                Stage("roof", "Roof", 3, Job("bring-planks", 12), Job("lay-floor", 2), Job("bring-reeds", 12), Job("thatch-roof", 3)),
            ],
            Technology: TechnologyCatalog.Warfare),
        new(BuildingKind.RopeBridge, "Rope bridge", "Posts on both rims, ropes and a plank deck across the ravine.", [],
            [
                Stage("posts", "Posts", 2, Job("bring-logs", 12), Job("raise-posts", 2)),
                Stage("ropes", "Ropes", 2, Job("bring-reeds", 16), Job("stretch-ropes", 3)),
                Stage("deck", "Deck", 3, Job("bring-logs", 16), Job("lay-deck", 3)),
            ]),
    ];

    private static readonly Dictionary<BuildingKind, BuildingDefinition> ByKind = Buildings.ToDictionary(b => b.Kind);
    private static readonly Dictionary<string, JobDefinition> JobsById = Jobs.ToDictionary(j => j.Id);

    public static BuildingDefinition Get(BuildingKind kind) => ByKind[kind];

    public static JobDefinition? FindJob(string id) => JobsById.GetValueOrDefault(id);

    public static BuildingKind? ForStorage(StorageKind storage) =>
        Buildings.FirstOrDefault(b => b.Storage == storage)?.Kind;
}
