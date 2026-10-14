using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Stats;

namespace Desertod.Domain.Technologies;

/// <summary>Technologies are grouped by era; the village's era is the latest era it has researched a technology of.</summary>
public enum Era
{
    Wild,
    Camp,
    Village,
    Town,
}

/// <param name="Multiplier">Applied to the task's yield factor.</param>
public sealed record YieldBonus(string TaskId, double Multiplier);

/// <summary>
/// A discovery the village makes together. The Tsar or a Governor puts a few to the vote and citizens choose the next one; citizens with the required skills research it
/// at its building, like an upgrade researched in a building. What it unlocks is declared on the buildings, tasks and roles
/// (their technology requirement); bonuses are declared here.
/// </summary>
/// <param name="ResearchedAt">Building where the research happens; the village must have one.</param>
/// <param name="Requires">Technologies to research first.</param>
/// <param name="Researcher">Skills a citizen needs to take part in the research.</param>
/// <param name="Cycles">Research work, in cycles of a researcher with every stat at 0.</param>
/// <param name="Crew">Different citizens who must research it before it is discovered.</param>
public sealed record TechnologyDefinition(
    string Id,
    string Name,
    string Description,
    Era Era,
    BuildingKind ResearchedAt,
    IReadOnlyList<string> Requires,
    IReadOnlyList<StatRequirement> Researcher,
    Stat Primary,
    Stat Secondary,
    int Cycles,
    int Crew,
    IReadOnlyList<YieldBonus> Bonuses);

public static class TechnologyCatalog
{
    /// <summary>Task id reported for a research cycle; the technology is reported separately.</summary>
    public const string ResearchTaskId = "research-technology";

    public const string StoneTools = "stone-tools";
    public const string HerbLore = "herb-lore";
    public const string Weaving = "weaving";
    public const string Tracking = "tracking";
    public const string Agriculture = "agriculture";
    public const string Carpentry = "carpentry";
    public const string Mining = "mining";
    public const string Writing = "writing";
    public const string Brewing = "brewing";
    public const string Baking = "baking";
    public const string Smelting = "smelting";
    public const string Music = "music";
    public const string Bookkeeping = "bookkeeping";
    public const string Mechanics = "mechanics";
    public const string Alchemy = "alchemy";
    public const string Forging = "forging";
    public const string WeatherLore = "weather-lore";
    public const string VigorDrinks = "vigor-drinks";
    public const string Weaponcraft = "weaponcraft";
    public const string Archery = "archery";
    public const string Warfare = "warfare";

    private static StatRequirement Needs(Stat stat, int minimum) => new(stat, minimum);

    private static YieldBonus Bonus(string taskId, double multiplier) => new(taskId, multiplier);

    /// <summary>
    /// Requirements follow the experience curve: laborer tasks train a stat to about 4 at full experience, so the Camp and
    /// Village eras ask for 2 to 4 and the Town era needs the crafts the Village era unlocks.
    /// </summary>
    public static IReadOnlyList<TechnologyDefinition> Technologies { get; } =
    [
        new(StoneTools, "Stone tools", "Sharp stones bound to handles. Wood and stone come easier.",
            Era.Camp, BuildingKind.StonePile, [], [Needs(Stat.Strength, 2)], Stat.Strength, Stat.Dexterity, 4, 2,
            [Bonus("chop-wood", 1.25), Bonus("quarry-stone", 1.25)]),
        new(HerbLore, "Herb lore", "Which berries feed and which roots heal. Foraging gives more, and poultices close wounds.",
            Era.Camp, BuildingKind.DryingRack, [], [Needs(Stat.Intelligence, 2)], Stat.Intelligence, Stat.Dexterity, 4, 2,
            [Bonus("forage", 1.25), Bonus("study-plants", 1.25)]),
        new(Weaving, "Weaving", "Reeds twisted into cords and mats. Reeds are cut faster.",
            Era.Camp, BuildingKind.LeanTo, [], [Needs(Stat.Dexterity, 2)], Stat.Dexterity, Stat.Intelligence, 4, 2,
            [Bonus("cut-reeds", 1.25)]),
        new(Tracking, "Tracking", "Reading hoof prints and broken twigs. Hunters bring back more.",
            Era.Camp, BuildingKind.DryingRack, [], [Needs(Stat.Dexterity, 2), Needs(Stat.Intelligence, 1)], Stat.Dexterity, Stat.Intelligence, 4, 2,
            [Bonus("hunt", 1.2), Bonus("track-game", 1.2), Bonus("hunt-birds", 1.2)]),
        new(Weaponcraft, "Weaponcraft", "Fire-hardened spears and hide armor. Wolves think twice.",
            Era.Camp, BuildingKind.StonePile, [StoneTools], [Needs(Stat.Strength, 2), Needs(Stat.Dexterity, 1)], Stat.Strength, Stat.Dexterity, 4, 2,
            []),
        new(Archery, "Archery", "A bent stick, a gut string and feathered arrows. Beasts are struck before they reach you.",
            Era.Camp, BuildingKind.DryingRack, [Tracking, Weaponcraft], [Needs(Stat.Dexterity, 2), Needs(Stat.Intelligence, 1)], Stat.Dexterity, Stat.Intelligence, 4, 2,
            []),

        new(Agriculture, "Agriculture", "Seeds put in the ground come back as grain. The tribe can plan a farm.",
            Era.Village, BuildingKind.Granary, [HerbLore], [Needs(Stat.Endurance, 3), Needs(Stat.Intelligence, 2)], Stat.Endurance, Stat.Intelligence, 8, 3,
            []),
        new(Carpentry, "Carpentry", "Logs become beams and planks. The tribe can plan a workshop, and carpenters appear.",
            Era.Village, BuildingKind.Woodyard, [StoneTools], [Needs(Stat.Dexterity, 3), Needs(Stat.Strength, 2)], Stat.Dexterity, Stat.Strength, 8, 3,
            []),
        new(Mining, "Mining", "Digging deep for coal and ore. The tribe can plan a mine, and miners appear.",
            Era.Village, BuildingKind.StoneYard, [StoneTools], [Needs(Stat.Strength, 4)], Stat.Strength, Stat.Endurance, 8, 3,
            []),
        new(Writing, "Writing", "Stories carved on birch bark outlive their tellers. The tribe can plan a library, and scholars appear.",
            Era.Village, BuildingKind.Hut, [], [Needs(Stat.Intelligence, 3), Needs(Stat.Charisma, 2)], Stat.Intelligence, Stat.Charisma, 8, 3,
            []),
        new(Brewing, "Brewing", "Grain and honey turn into kvass and mead. The tribe can plan a tavern.",
            Era.Village, BuildingKind.Granary, [Agriculture], [Needs(Stat.Charisma, 3)], Stat.Charisma, Stat.Intelligence, 8, 3,
            []),
        new(Baking, "Baking", "Ground grain kneaded and baked in a clay oven. The tribe can plan a bakery, and bakers appear.",
            Era.Village, BuildingKind.Granary, [Agriculture], [Needs(Stat.Dexterity, 3), Needs(Stat.Intelligence, 2)], Stat.Dexterity, Stat.Intelligence, 8, 3,
            []),
        new(WeatherLore, "Weather lore", "Clouds, wind and aching knees tell what the sky will do. Synoptics appear; reading the sky gives more.",
            Era.Village, BuildingKind.Granary, [Agriculture], [Needs(Stat.Intelligence, 3), Needs(Stat.Endurance, 2)], Stat.Intelligence, Stat.Endurance, 8, 3,
            [Bonus("read-sky", 1.25)]),

        new(Smelting, "Smelting", "Ore and coal in a hot furnace give iron. The tribe can plan a smithy, and smiths appear.",
            Era.Town, BuildingKind.Mine, [Mining], [Needs(Stat.Strength, 5), Needs(Stat.Intelligence, 3)], Stat.Strength, Stat.Intelligence, 12, 3,
            []),
        new(Music, "Music", "Gusli strings and songs of old. The tribe can plan a stage, and bards appear.",
            Era.Town, BuildingKind.Tavern, [Brewing], [Needs(Stat.Charisma, 4), Needs(Stat.Dexterity, 3)], Stat.Charisma, Stat.Dexterity, 12, 3,
            []),
        new(Bookkeeping, "Bookkeeping", "Tallies of every sack and barrel. The tribe can plan a storehouse; research goes faster.",
            Era.Town, BuildingKind.Library, [Writing], [Needs(Stat.Intelligence, 5)], Stat.Intelligence, Stat.Charisma, 12, 3,
            [Bonus("research", 1.25)]),
        new(Mechanics, "Mechanics", "Gears, levers and axles. Artisans appear; planks are sawn faster.",
            Era.Town, BuildingKind.Workshop, [Carpentry, Smelting], [Needs(Stat.Intelligence, 5), Needs(Stat.Dexterity, 5)], Stat.Intelligence, Stat.Dexterity, 12, 3,
            [Bonus("saw-planks", 1.25)]),
        new(Alchemy, "Alchemy", "Herbs boiled and distilled into remedies. Alchemists appear.",
            Era.Town, BuildingKind.Library, [Writing, HerbLore], [Needs(Stat.Intelligence, 6)], Stat.Intelligence, Stat.Dexterity, 12, 3,
            []),
        new(Forging, "Forging", "Iron folded and hammered into chains. Master smiths appear; iron is smelted faster.",
            Era.Town, BuildingKind.Smithy, [Smelting], [Needs(Stat.Strength, 6), Needs(Stat.Dexterity, 4)], Stat.Strength, Stat.Dexterity, 12, 3,
            [Bonus("smelt-ingot", 1.25)]),
        new(Warfare, "Warfare", "Iron swords, oak shields and drills on the yard. The tribe can plan barracks; guards and captains appear.",
            Era.Town, BuildingKind.Smithy, [Smelting, Weaponcraft], [Needs(Stat.Strength, 5), Needs(Stat.Endurance, 3)], Stat.Strength, Stat.Endurance, 12, 3,
            []),
        new(VigorDrinks, "Vigor drinks",
            "Kvass, honey, beans from a passing merchant and three scholars who have not slept since Tuesday. Tastes like a thunderstorm. Brewed at the tavern; a drink gives energy back.",
            Era.Town, BuildingKind.Tavern, [Brewing, Alchemy, Bookkeeping], [Needs(Stat.Charisma, 4), Needs(Stat.Intelligence, 4)],
            Stat.Intelligence, Stat.Charisma, 12, 3,
            []),
    ];

    private static readonly Dictionary<string, TechnologyDefinition> ById = Technologies.ToDictionary(t => t.Id);

    public static TechnologyDefinition? Find(string id) => ById.GetValueOrDefault(id);

    public static TechnologyDefinition Get(string id) => ById[id];
}
