using Desertod.Domain.Chat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Desertod.Infrastructure.Data;

public sealed class DesertodDbContext(DbContextOptions<DesertodDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<PasswordLogin> PasswordLogins => Set<PasswordLogin>();
    public DbSet<Village> Villages => Set<Village>();
    public DbSet<Citizen> Citizens => Set<Citizen>();
    public DbSet<VillageProject> VillageProjects => Set<VillageProject>();
    public DbSet<VillageQuest> VillageQuests => Set<VillageQuest>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<DungeonRun> DungeonRuns => Set<DungeonRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(account =>
        {
            account.HasKey(a => a.Id);
            account.Property(a => a.DisplayName).HasMaxLength(64);
            account.HasMany(a => a.Logins).WithOne().HasForeignKey(l => l.AccountId);
            account.OwnsOne(a => a.Character, character =>
            {
                character.ToJson();
                character.OwnsOne(c => c.Appearance, appearance => appearance.Property(a => a.Gender).HasConversion<string>());
            });
        });

        modelBuilder.Entity<ExternalLogin>(login =>
        {
            login.HasKey(l => new { l.Provider, l.ProviderUserId });
            login.Property(l => l.Provider).HasMaxLength(16);
            login.Property(l => l.ProviderUserId).HasMaxLength(128);
            login.Property(l => l.ProviderUserName).HasMaxLength(128);
        });

        modelBuilder.Entity<PasswordLogin>(login =>
        {
            login.HasKey(l => l.Email);
            login.Property(l => l.Email).HasMaxLength(PasswordLogin.EmailMaximumLength);
            login.Property(l => l.PasswordHash).HasMaxLength(256);
            login.Property(l => l.RegisteredFrom).HasMaxLength(PasswordLogin.AddressMaximumLength);
            login.HasIndex(l => l.AccountId).IsUnique();
            login.HasIndex(l => new { l.RegisteredFrom, l.RegisteredAt });
            login.HasOne<Account>().WithMany().HasForeignKey(l => l.AccountId);
        });

        modelBuilder.Entity<Village>(village =>
        {
            village.HasKey(v => v.Id);
            village.HasIndex(v => v.Slug).IsUnique();
            village.Property(v => v.Slug).HasMaxLength(48);
            village.Property(v => v.Name).HasMaxLength(Village.NameMaximumLength);
            village.Property(v => v.Description).HasMaxLength(Village.DescriptionMaximumLength);
            village.Property(v => v.InviteCode).HasMaxLength(Village.InviteCodeLength);
            village.HasIndex(v => v.IsPrivate);
            village.HasOne<Account>().WithMany().HasForeignKey(v => v.OwnerAccountId);
            village.OwnsMany(v => v.Storages, storage =>
            {
                storage.ToJson();
                storage.Property(s => s.Kind).HasConversion<string>();
                storage.Ignore(s => s.Definition);
                storage.OwnsMany(s => s.Goods, good => good.Property(g => g.Kind).HasConversion<string>());
            });
            village.OwnsMany(v => v.Structures, structure =>
            {
                structure.ToJson();
                structure.Property(s => s.Kind).HasConversion<string>();
            });
            village.OwnsMany(v => v.Sites, site =>
            {
                site.ToJson();
                site.Property(s => s.Kind).HasConversion<string>();
                site.Ignore(s => s.Definition);
                site.Ignore(s => s.IsFinished);
                site.Ignore(s => s.HasWork);
                site.OwnsMany(s => s.Progress);
                site.OwnsMany(s => s.Hands);
            });
            village.OwnsMany(v => v.Wildlife, herd =>
            {
                herd.ToJson();
                herd.Property(h => h.Kind).HasConversion<string>();
                herd.Ignore(h => h.Definition);
            });
            village.OwnsMany(v => v.Trees, tree =>
            {
                tree.ToJson();
                tree.Ignore(t => t.Tile);
            });
            village.OwnsMany(v => v.Fields, bed =>
            {
                bed.ToJson();
                bed.Property(b => b.Crop).HasConversion<string>();
            });
            village.OwnsMany(v => v.Research, research =>
            {
                research.ToJson();
                research.OwnsMany(r => r.Hands);
            });
            village.OwnsOne(v => v.Poll, poll =>
            {
                poll.ToJson();
                poll.Ignore(p => p.IsOpen);
                poll.OwnsMany(p => p.Ballots);
            });
            village.OwnsMany(v => v.Forecasts, forecast => forecast.ToJson());
            village.OwnsMany(v => v.Raids, raid =>
            {
                raid.ToJson();
                raid.Property(r => r.Source).HasConversion<string>();
                raid.Property(r => r.Enemy).HasConversion<string>();
                raid.Property(r => r.Outcome).HasConversion<string>();
                raid.Ignore(r => r.IsPending);
                raid.OwnsMany(r => r.Stolen, good => good.Property(g => g.Kind).HasConversion<string>());
                raid.OwnsMany(r => r.Loot, good => good.Property(g => g.Kind).HasConversion<string>());
            });
            village.OwnsOne(v => v.Wilds, wilds =>
            {
                wilds.ToJson();
                wilds.Ignore(w => w.CaveMouth);
                wilds.Ignore(w => w.Lookout);
                wilds.OwnsMany(w => w.Ravine);
                wilds.OwnsMany(w => w.Passes);
                wilds.OwnsMany(w => w.Obstacles, obstacle =>
                {
                    obstacle.Property(o => o.Kind).HasConversion<string>();
                    obstacle.Ignore(o => o.Tile);
                    obstacle.Ignore(o => o.Center);
                });
            });
            village.HasIndex(v => v.NextRaidAt);
            village.Property(v => v.ChosenTechnology).HasMaxLength(32);
            village.Ignore(v => v.TsarSpot);
            village.OwnsOne(v => v.TsarTrip, MapTrip);
            village.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<Citizen>(citizen =>
        {
            citizen.HasKey(c => c.Id);
            citizen.HasIndex(c => new { c.VillageId, c.AccountId }).IsUnique();
            citizen.HasIndex(c => new { c.CycleSettled, c.CycleEndsAt });
            citizen.HasIndex(c => c.IsCarrying);
            citizen.OwnsMany(c => c.Load, load =>
            {
                load.ToJson();
                load.Property(g => g.Kind).HasConversion<string>();
            });
            citizen.OwnsMany(c => c.Skills, skills =>
            {
                skills.ToJson();
                skills.Property(s => s.Kind).HasConversion<string>();
            });
            citizen.OwnsOne(c => c.Appearance, appearance =>
            {
                appearance.ToJson();
                appearance.Property(a => a.Gender).HasConversion<string>();
            });
            citizen.Property(c => c.Name).HasMaxLength(32);
            citizen.Property(c => c.Subtitle).HasMaxLength(Citizen.SubtitleMaximumLength);
            citizen.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            citizen.Property(c => c.CurrentTaskId).HasMaxLength(32);
            citizen.Property(c => c.CurrentSiteId).HasMaxLength(16);
            citizen.Property(c => c.CurrentTechnologyId).HasMaxLength(32);
            citizen.Property(c => c.Weapon).HasConversion<string>().HasMaxLength(16);
            citizen.Property(c => c.Armor).HasConversion<string>().HasMaxLength(16);
            citizen.Ignore(c => c.RegenerationMultiplier);
            citizen.Ignore(c => c.SleepsUnderTrees);
            citizen.Ignore(c => c.Spot);
            citizen.Ignore(c => c.IdleSpot);
            citizen.OwnsOne(c => c.Trip, MapTrip);
            citizen.HasOne<Village>().WithMany().HasForeignKey(c => c.VillageId);
            citizen.HasOne<Account>().WithMany().HasForeignKey(c => c.AccountId);
            citizen.OwnsOne(c => c.Stats, stats => stats.Ignore(s => s.Total));
            citizen.Ignore(c => c.MaximumEnergy);
            citizen.Ignore(c => c.GraceEndsAt);
            citizen.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<VillageProject>(project =>
        {
            project.HasKey(p => p.Id);
            project.HasIndex(p => new { p.VillageId, p.Status });
            project.Property(p => p.DefinitionId).HasMaxLength(32);
            project.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            project.HasOne<Village>().WithMany().HasForeignKey(p => p.VillageId);
            project.HasMany(p => p.Materials).WithOne().HasForeignKey(m => m.VillageProjectId);
            project.HasMany(p => p.Contributions).WithOne().HasForeignKey(c => c.VillageProjectId);
            project.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<ProjectMaterial>(material =>
        {
            material.HasKey(m => new { m.VillageProjectId, m.Kind });
            material.Property(m => m.Kind).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<ProjectContribution>(contribution =>
        {
            contribution.HasKey(c => new { c.VillageProjectId, c.CitizenId, c.Key });
            contribution.Property(c => c.Key).HasMaxLength(32);
            contribution.HasOne<Citizen>().WithMany().HasForeignKey(c => c.CitizenId);
        });

        modelBuilder.Entity<VillageQuest>(quest =>
        {
            quest.HasKey(q => q.Id);
            quest.HasIndex(q => new { q.VillageId, q.Key })
                .IsUnique()
                .HasFilter($"\"{nameof(VillageQuest.Status)}\" = '{nameof(QuestStatus.Active)}'");
            quest.Property(q => q.Kind).HasConversion<string>().HasMaxLength(16);
            quest.Property(q => q.Resource).HasConversion<string>().HasMaxLength(32);
            quest.Property(q => q.Key).HasMaxLength(48);
            quest.Property(q => q.Status).HasConversion<string>().HasMaxLength(16);
            quest.HasOne<Village>().WithMany().HasForeignKey(q => q.VillageId);
            quest.HasMany(q => q.Contributions).WithOne().HasForeignKey(c => c.VillageQuestId);
            quest.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<QuestContribution>(contribution =>
        {
            contribution.HasKey(c => new { c.VillageQuestId, c.CitizenId });
            contribution.HasOne<Citizen>().WithMany().HasForeignKey(c => c.CitizenId);
        });

        modelBuilder.Entity<ChatMessage>(message =>
        {
            message.HasKey(m => m.Id);
            message.HasIndex(m => new { m.VillageId, m.SentAt });
            message.Property(m => m.AuthorName).HasMaxLength(ChatMessage.AuthorNameMaximumLength);
            message.Property(m => m.Text).HasMaxLength(ChatMessage.TextMaximumLength);
            message.Ignore(m => m.IsTsar);
            message.HasOne<Village>().WithMany().HasForeignKey(m => m.VillageId);
            message.HasOne<Account>().WithMany().HasForeignKey(m => m.AccountId);
            message.HasOne<Citizen>().WithMany().HasForeignKey(m => m.CitizenId);
        });

        modelBuilder.Entity<DungeonRun>(run =>
        {
            run.HasKey(r => r.Id);
            run.HasIndex(r => new { r.VillageId, r.Phase });
            run.Property(r => r.DungeonId).HasMaxLength(32);
            run.Property(r => r.Phase).HasConversion<string>().HasMaxLength(16);
            run.Property(r => r.Outcome).HasConversion<string>().HasMaxLength(16);
            run.Ignore(r => r.IsActive);
            run.Ignore(r => r.Tsar);
            run.HasOne<Village>().WithMany().HasForeignKey(r => r.VillageId);
            run.OwnsMany(r => r.Heroes, hero =>
            {
                hero.ToJson();
                hero.Property(h => h.Class).HasConversion<string>();
                hero.Property(h => h.Killer).HasConversion<string>();
                hero.PrimitiveCollection(h => h.Broken).ElementType(e => e.HasConversion<string>());
                hero.Ignore(h => h.IsTsar);
                hero.OwnsMany(h => h.Loot, good => good.Property(g => g.Kind).HasConversion<string>());
            });
            run.Property<uint>("Version").IsRowVersion();
        });
    }

    private static void MapTrip<TOwner>(OwnedNavigationBuilder<TOwner, Trip> trip)
        where TOwner : class
    {
        trip.ToJson();
        trip.Ignore(t => t.From);
        trip.Ignore(t => t.To);
        trip.Ignore(t => t.ArrivesAt);
    }
}
