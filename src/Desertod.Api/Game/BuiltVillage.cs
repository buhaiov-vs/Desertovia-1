using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Farming;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Storage;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;

namespace Desertod.Api.Game;

/// <summary>
/// Development only: a village with every building, storage and project finished, every technology discovered and the
/// whole land explored, so their pictures can be checked side by side. It is rebuilt on every start; its Tsar is a development login.
/// </summary>
public static class BuiltVillage
{
    public const string Slug = "demo-built";
    private const string Name = "Демо: Отстроенная деревня";
    private const int StockPerGood = 40;
    private const int Knowledge = 200;

    /// <summary>Natural places left free: the clearing around the campfire, the trail to the gate, berries and stones.</summary>
    private static readonly TileArea[] KeptClear =
    [
        new(12, 10, 7, 7),
        new(0, 12, 12, 3),
        new(1, 16, 7, 5),
        new(8, 1, 5, 4),
    ];

    private static readonly (CropKind Crop, double Growth)[] Beds = [(CropKind.Rye, 0.2), (CropKind.Cabbage, 0.6), (CropKind.Flax, 1)];

    public static async Task SeedAsync(DesertodDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        await DemoVillage.RemoveAsync(db, Slug, cancellationToken);
        var now = time.GetUtcNow();

        var tsar = await DemoVillage.AccountAsync(db, DevelopmentRoles.TsarName, now, cancellationToken);
        var governorAccount = await DemoVillage.AccountAsync(db, DevelopmentRoles.GovernorName, now, cancellationToken);
        var villagerAccount = await DemoVillage.AccountAsync(db, DevelopmentRoles.VillagerName, now, cancellationToken);
        EnsureCharacter(tsar, DevelopmentRoles.TsarName, Gender.Male, 1);
        EnsureCharacter(governorAccount, DevelopmentRoles.GovernorName, Gender.Male, 2);
        EnsureCharacter(villagerAccount, DevelopmentRoles.VillagerName, Gender.Female, 0);
        var village = Village.Create(Slug, Name, "Все здания, склады и стройки готовы: посмотреть, как они выглядят.", false, tsar.Id, now);

        using var plots = FreePlots().GetEnumerator();
        string NextPlot() => plots.MoveNext() ? plots.Current : throw new InvalidOperationException("The camp has no free plot left.");

        foreach (var storage in StorageCatalog.Storages.Where(s => s.Kind != StorageKind.GreatGranary))
        {
            village.Storages.Add(StorageBuilding.Create(storage.Kind, NextPlot(), now));
        }

        foreach (var building in BuildingCatalog.Buildings.Where(b => b.Storage is null && b.Kind != BuildingKind.RopeBridge))
        {
            village.Structures.Add(new Structure { Kind = building.Kind, PlotId = NextPlot(), BuiltAt = now });
        }

        if (BridgePlot(village) is { } bridge)
        {
            village.Structures.Add(new Structure { Kind = BuildingKind.RopeBridge, PlotId = bridge, BuiltAt = now });
        }

        village.Explored = Enumerable.Repeat(byte.MaxValue, Exploration.Size).ToArray();

        FarmRules.EnsureBeds(village);
        foreach (var (bed, (crop, growth)) in village.Fields.OrderBy(b => b.Index).Zip(Beds))
        {
            bed.Crop = crop;
            bed.Growth = growth;
            bed.UpdatedAt = now;
        }

        foreach (var technology in TechnologyCatalog.Technologies)
        {
            village.Research.Add(new TechnologyProgress { TechnologyId = technology.Id, Done = technology.Cycles, ResearchedAt = now });
        }

        foreach (var project in ProjectCatalog.Projects.Where(p => p.Reward.Storage is not null))
        {
            village.Storages.Add(StorageBuilding.Create(project.Reward.Storage!.Value, project.Id, now));
        }

        foreach (var kind in Enum.GetValues<ResourceKind>().Where(StorageCatalog.IsGood))
        {
            village.Store(kind, StockPerGood);
        }

        // Swords, shields, arrows and armor are last in the stock loop and do not fit the storehouse total.
        // Overflow stays withdrawable, so every dungeon class can still take its gear.
        var armory = village.Storages.Single(storage => storage.Kind == StorageKind.Storehouse);
        foreach (var kind in new[] { ResourceKind.Arrows, ResourceKind.Sword, ResourceKind.Shield, ResourceKind.LeatherArmor })
        {
            var missing = StockPerGood - village.Stock(kind);
            if (missing <= 0)
            {
                continue;
            }

            var pile = armory.Goods.SingleOrDefault(good => good.Kind == kind && good.Sharpen == 0);
            if (pile is null)
            {
                pile = new StoredGood { Kind = kind, Sharpen = 0 };
                armory.Goods.Add(pile);
            }

            pile.Amount += missing;
        }

        village.Store(ResourceKind.ResearchPoints, Knowledge);
        db.Villages.Add(village);

        var governor = CharacterCreation.Create(village.Id, governorAccount.Id, governorAccount.Character!, now);
        governor.IsGovernor = true;
        var villager = CharacterCreation.Create(village.Id, villagerAccount.Id, villagerAccount.Character!, now);
        var residents = new List<Citizen> { governor, villager };
        foreach (var player in DevelopmentRoles.Players)
        {
            var account = await DemoVillage.AccountAsync(db, player.Name, now, cancellationToken);
            EnsureCharacter(account, player.Name, player.Gender, player.Style, player.SkinTone, player.HairColor, player.OutfitColor);
            residents.Add(CharacterCreation.Create(village.Id, account.Id, account.Character!, now));
        }

        foreach (var resident in residents)
        {
            ReadyForDungeon(resident);
        }

        db.Citizens.AddRange(residents);
        ShelterRules.Assign(village, residents, now);

        foreach (var kind in StoryQuests.Chain)
        {
            var quest = VillageQuest.Open(village.Id, kind, null, StoryQuests.Target(kind), now);
            quest.Status = QuestStatus.Completed;
            quest.CompletedAt = now;
            db.VillageQuests.Add(quest);
        }

        foreach (var definition in ProjectCatalog.Projects)
        {
            var finished = VillageProject.Start(village.Id, definition, now);
            foreach (var material in definition.Materials)
            {
                finished.Materials.Single(m => m.Kind == material.Resource).Delivered = material.Amount;
            }

            finished.SurveyProgress = definition.SurveyCycles;
            finished.BuildProgress = definition.BuildCycles;
            finished.Status = ProjectStatus.Completed;
            finished.CompletedAt = now;
            db.VillageProjects.Add(finished);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Plots of the camp that keep clear of reserved ground and natural places, nearest the campfire first.</summary>
    private static IEnumerable<string> FreePlots()
    {
        var area = WorldMap.BuildArea;
        var camp = WorldMap.Places[WorldMap.Camp];
        var plots = new List<string>();
        for (var y = area.Y; y + WorldMap.PlotHeight <= area.Y + area.Height; y += WorldMap.PlotHeight)
        {
            for (var x = area.X; x + WorldMap.PlotWidth <= area.X + area.Width; x += WorldMap.PlotWidth)
            {
                var footprint = WorldMap.Footprint(x, y);
                if (!WorldMap.ReservedAreas.Concat(KeptClear).Any(taken => taken.Overlaps(footprint)))
                {
                    plots.Add(WorldMap.PlotAt(x, y));
                }
            }
        }

        return plots.OrderBy(plotId => WorldMap.Yard(plotId).DistanceTo(camp));
    }

    /// <summary>The spot nearest the campfire where a rope bridge spans the ravine, or null when there is none.</summary>
    private static string? BridgePlot(Village village)
    {
        var camp = WorldMap.Places[WorldMap.Camp];
        return (village.Wilds?.Ravine ?? [])
            .SelectMany(span => Enumerable.Range(span.Top + span.Height - WorldMap.PlotHeight + 1, WorldMap.PlotHeight)
                .Select(y => (X: span.X, Y: y)))
            .Where(plot => Wilderness.Spans(village, WorldMap.Footprint(plot.X, plot.Y)))
            .Select(plot => WorldMap.PlotAt(plot.X, plot.Y))
            .OrderBy(plotId => WorldMap.Yard(plotId).DistanceTo(camp))
            .FirstOrDefault();
    }

    /// <summary>A ready look, so the development login can enter the village without the character form.</summary>
    private static void EnsureCharacter(Account account, string name, Gender gender, int style, int skinTone = 2, int hairColor = 1, int outfitColor = 3)
    {
        account.Character ??= CharacterCreation.CreateCharacter(name, new Appearance
        {
            Gender = gender,
            Style = style,
            SkinTone = skinTone,
            HairColor = hairColor,
            OutfitColor = outfitColor,
        });
    }

    /// <summary>
    /// Enough stats and crafts to be offered every citizen class: brawler, spear, sword, bow, shield and healer.
    /// </summary>
    private static void ReadyForDungeon(Citizen citizen)
    {
        citizen.Stats.Strength = 4;
        citizen.Stats.Dexterity = 4;
        citizen.Stats.Intelligence = 4;
        citizen.Stats.Endurance = 4;
        citizen.StoredEnergy = Energy.Maximum(citizen.Stats.Endurance);
        citizen.Skills =
        [
            new SkillProgress { Kind = Skill.Guarding, Level = 2 },
            new SkillProgress { Kind = Skill.Hunting, Level = 2 },
            new SkillProgress { Kind = Skill.Scholarship, Level = 2 },
        ];
    }
}
