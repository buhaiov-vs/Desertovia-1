using Desertod.Api.Auth;
using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.Storage;
using Desertod.Domain.World;
using Desertod.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Api.Game;

/// <summary>
/// Development only: a village half way through the story, with the Tsar Bridge under construction and citizens at
/// work, so the bridge and its panel can be seen without hours of play. It is rebuilt on every
/// start; its Tsar and citizens are development logins, so anyone can sign in as them by name.
/// </summary>
public static class DemoVillage
{
    public const string Slug = "demo-bridge";
    private const string Name = "Демо: Царский мост";
    private const string TsarName = "Демо-царь";

    /// <param name="SkillLevel">Level of the craft of the resident's task; within what their stats allow.</param>
    private sealed record Resident(
        string Name, string TaskId, int Strength, int Dexterity, int Intelligence, int Charisma, int Endurance, int SkillLevel);

    private static readonly Resident[] Residents =
    [
        new("Ярослав", "supervise", 2, 1, 5, 2, 2, 4),
        new("Добрыня", "build", 4, 1, 1, 1, 3, 2),
        new("Тихон", "build", 3, 2, 1, 1, 3, 2),
        new("Алёна", "deliver-wood", 2, 3, 2, 2, 3, 3),
        new("Василиса", "deliver-stone", 3, 2, 4, 2, 2, 2),
        new("Ульяна", "deliver-reeds", 1, 3, 2, 3, 3, 2),
    ];

    private static readonly QuestKind[] DoneQuests =
    [
        QuestKind.Fire, QuestKind.Shelter, QuestKind.Stores, QuestKind.FirstHouse,
        QuestKind.Monument, QuestKind.FishingSpot, QuestKind.Palisade,
    ];

    private static readonly string[] DoneProjects = [ProjectCatalog.TsarMonument, ProjectCatalog.FishingPier, ProjectCatalog.Palisade];

    public static async Task SeedAsync(DesertodDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        await RemoveAsync(db, Slug, cancellationToken);
        var now = time.GetUtcNow();

        var tsar = await AccountAsync(db, TsarName, now, cancellationToken);
        var village = Village.Create(Slug, Name, "Посмотреть стройку Царского моста без долгой игры.", false, tsar.Id, now);
        village.Structures.AddRange(
        [
            new Structure { Kind = BuildingKind.Hut, PlotId = "13,5", BuiltAt = now },
            new Structure { Kind = BuildingKind.Hut, PlotId = "17,5", BuiltAt = now },
            new Structure { Kind = BuildingKind.Hut, PlotId = "21,5", BuiltAt = now },
            new Structure { Kind = BuildingKind.LeanTo, PlotId = "9,5", BuiltAt = now },
        ]);
        village.Storages.Add(StorageBuilding.Create(StorageKind.Heap, "17,12", now));
        village.Storages.Add(StorageBuilding.Create(StorageKind.WoodPile, "21,12", now));
        village.Storages.Add(StorageBuilding.Create(StorageKind.StonePile, "21,17", now));
        village.Store(ResourceKind.Wood, 60);
        village.Store(ResourceKind.Stone, 60);
        village.Store(ResourceKind.Reeds, 20);
        village.Store(ResourceKind.Food, 20);
        db.Villages.Add(village);

        var citizens = new List<Citizen>();
        foreach (var resident in Residents)
        {
            var account = await AccountAsync(db, resident.Name, now, cancellationToken);
            var citizen = CharacterCreation.Create(village.Id, account.Id, resident.Name, now);
            citizen.Stats = new StatBlock
            {
                Strength = resident.Strength,
                Dexterity = resident.Dexterity,
                Intelligence = resident.Intelligence,
                Charisma = resident.Charisma,
                Endurance = resident.Endurance,
            };
            citizen.Skills.Add(new SkillProgress { Kind = GameCatalog.FindTask(resident.TaskId)!.Skill, Level = resident.SkillLevel });
            citizen.StoredEnergy = citizen.MaximumEnergy;
            citizen.IsGovernor = resident.TaskId == "supervise";
            citizens.Add(citizen);
        }

        db.Citizens.AddRange(citizens);
        ShelterRules.Assign(village, citizens, now);

        foreach (var kind in DoneQuests)
        {
            var quest = VillageQuest.Open(village.Id, kind, null, StoryQuests.Target(kind), now);
            quest.Status = QuestStatus.Completed;
            quest.CompletedAt = now;
            db.VillageQuests.Add(quest);
        }

        db.VillageQuests.Add(VillageQuest.Open(village.Id, QuestKind.TsarBridge, null, StoryQuests.Target(QuestKind.TsarBridge), now));

        var completed = new HashSet<string>(DoneProjects);
        foreach (var id in DoneProjects)
        {
            var finished = VillageProject.Start(village.Id, ProjectCatalog.Find(id)!, now);
            foreach (var material in finished.Definition.Materials)
            {
                finished.Materials.Single(m => m.Kind == material.Resource).Delivered = material.Amount;
            }

            finished.SurveyProgress = finished.Definition.SurveyCycles;
            finished.BuildProgress = finished.Definition.BuildCycles;
            finished.Status = ProjectStatus.Completed;
            finished.CompletedAt = now;
            db.VillageProjects.Add(finished);
        }

        var bridge = VillageProject.Start(village.Id, ProjectCatalog.Find(ProjectCatalog.TsarBridge)!, now);
        Share(bridge, citizens, VillageProject.SurveyKey, bridge.Definition.SurveyCycles);
        Share(bridge, citizens, nameof(ResourceKind.Wood), 150);
        Share(bridge, citizens, nameof(ResourceKind.Stone), 70);
        Share(bridge, citizens, nameof(ResourceKind.Reeds), 40);
        Share(bridge, citizens, VillageProject.BuildKey, 24);
        db.VillageProjects.Add(bridge);

        var supervision = 0;
        var builders = 0;
        foreach (var (citizen, resident) in citizens.Zip(Residents))
        {
            var context = new WorkContext
            {
                Village = village,
                Project = bridge,
                CompletedProjects = completed,
                ActiveCitizenCount = citizens.Count,
                RecentCitizenCount = citizens.Count,
                SupervisionCapacity = supervision,
                BuildersWorking = builders,
            };
            CycleEngine.Start(citizen, GameCatalog.FindTask(resident.TaskId)!, context, now);
            if (resident.TaskId == "supervise")
            {
                supervision += ProjectRules.SupervisionCapacity(citizen.Stats.Intelligence);
            }
            else if (resident.TaskId == "build")
            {
                builders++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Spreads earlier work evenly, so nobody is over the share cap; the first citizens take the remainder.</summary>
    private static void Share(VillageProject project, IReadOnlyList<Citizen> citizens, string key, int amount)
    {
        for (var index = 0; index < citizens.Count; index++)
        {
            project.Record(citizens[index].Id, key, amount / citizens.Count + (index < amount % citizens.Count ? 1 : 0));
        }
    }

    internal static async Task RemoveAsync(DesertodDbContext db, string slug, CancellationToken cancellationToken)
    {
        var village = await db.Villages.SingleOrDefaultAsync(v => v.Slug == slug, cancellationToken);
        if (village is null)
        {
            return;
        }

        var citizenIds = db.Citizens.Where(c => c.VillageId == village.Id).Select(c => c.Id);
        var projectIds = db.VillageProjects.Where(p => p.VillageId == village.Id).Select(p => p.Id);
        var questIds = db.VillageQuests.Where(q => q.VillageId == village.Id).Select(q => q.Id);
        await db.Set<ProjectContribution>().Where(c => projectIds.Contains(c.VillageProjectId)).ExecuteDeleteAsync(cancellationToken);
        await db.Set<ProjectMaterial>().Where(m => projectIds.Contains(m.VillageProjectId)).ExecuteDeleteAsync(cancellationToken);
        await db.Set<QuestContribution>().Where(c => questIds.Contains(c.VillageQuestId)).ExecuteDeleteAsync(cancellationToken);
        await db.Set<ProjectContribution>().Where(c => citizenIds.Contains(c.CitizenId)).ExecuteDeleteAsync(cancellationToken);
        await db.Set<QuestContribution>().Where(c => citizenIds.Contains(c.CitizenId)).ExecuteDeleteAsync(cancellationToken);
        await db.VillageProjects.Where(p => p.VillageId == village.Id).ExecuteDeleteAsync(cancellationToken);
        await db.VillageQuests.Where(q => q.VillageId == village.Id).ExecuteDeleteAsync(cancellationToken);
        await db.ChatMessages.Where(m => m.VillageId == village.Id).ExecuteDeleteAsync(cancellationToken);
        await db.Citizens.Where(c => c.VillageId == village.Id).ExecuteDeleteAsync(cancellationToken);
        await db.Villages.Where(v => v.Id == village.Id).ExecuteDeleteAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    /// <summary>The development login with this name, as the login page would create it.</summary>
    internal static async Task<Account> AccountAsync(DesertodDbContext db, string name, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var userId = name.ToLowerInvariant();
        var login = await db.ExternalLogins.SingleOrDefaultAsync(
            l => l.Provider == AuthenticationSetup.DevelopmentProvider && l.ProviderUserId == userId, cancellationToken);
        if (login is not null)
        {
            return await db.Accounts.SingleAsync(a => a.Id == login.AccountId, cancellationToken);
        }

        var account = new Account { DisplayName = name, CreatedAt = now };
        account.Logins.Add(new ExternalLogin
        {
            Provider = AuthenticationSetup.DevelopmentProvider,
            ProviderUserId = userId,
            ProviderUserName = name,
            AccountId = account.Id,
            LinkedAt = now,
        });
        db.Accounts.Add(account);
        return account;
    }
}
