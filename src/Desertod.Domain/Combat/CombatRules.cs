using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Weather;

namespace Desertod.Domain.Combat;

/// <param name="Power">Fight power of the citizen, weapon included.</param>
/// <param name="EnemyPower">Fight power of the enemy.</param>
/// <param name="Loot">What the slain enemy left; stored like any other produce.</param>
/// <param name="Broken">Weapon or armor that broke in the fight.</param>
public sealed record FightReport(
    EnemyKind Enemy,
    bool Won,
    bool Injured,
    DateTimeOffset? InjuredUntil,
    double Power,
    double EnemyPower,
    ResourceKind? Weapon,
    ResourceKind? Armor,
    IReadOnlyList<ResourceAmount> Loot,
    IReadOnlyList<ResourceKind> Broken);

/// <summary>
/// Fights between citizens and beasts or the undead. A fight is won with the chance power / (power + enemy power);
/// a loss wounds the citizen unless their armor stops the blow. A wounded citizen cannot work until healed.
/// </summary>
public static class CombatRules
{
    public const double UnarmedPower = 2;
    public const double StrengthPower = 0.6;
    public const double DexterityPower = 0.4;
    public const double GuardingPower = 0.3;

    /// <summary>Beasts hunt in the dark: ambushes are this much more likely at night.</summary>
    public const double NightAmbushMultiplier = 1.5;

    /// <summary>Even a won fight may leave a wound.</summary>
    public const double WonInjuryChance = 0.1;

    /// <summary>A building at full damage works at half speed; see <see cref="BuildingMultiplier"/>.</summary>
    public const int MaximumDamage = 100;

    /// <summary>Skill experience for a fight: the same as a cycle of work, half of it for a lost fight.</summary>
    public const int FightExperience = GameRules.BaseExperiencePerCycle;

    /// <summary>A new village is left alone this long, so its first players learn the game in peace.</summary>
    public static TimeSpan PeacefulTime { get; private set; } = TimeSpan.FromDays(1);

    public static void Configure(TimeSpan peacefulTime) =>
        PeacefulTime = peacefulTime >= TimeSpan.Zero
            ? peacefulTime
            : throw new ArgumentOutOfRangeException(nameof(peacefulTime), peacefulTime, "The peaceful time cannot be negative.");

    public static bool IsPeaceful(Village village, DateTimeOffset now) => now < village.CreatedAt + PeacefulTime;

    public static bool IsInjured(Citizen citizen, DateTimeOffset now) => citizen.InjuredUntil is { } until && now < until;

    /// <summary>Fight power: strength and agility, training as a guard, and the weapon in hand.</summary>
    public static double Power(Citizen citizen) =>
        UnarmedPower
        + StrengthPower * citizen.Stats.Strength
        + DexterityPower * citizen.Stats.Dexterity
        + GuardingPower * citizen.SkillLevel(Skill.Guarding)
        + (UsableWeapon(citizen)?.Power ?? 0);

    /// <summary>The weapon in hand, if it can be used: a bow needs arrows.</summary>
    public static WeaponDefinition? UsableWeapon(Citizen citizen) =>
        ArmoryCatalog.Weapon(citizen.Weapon) is { } weapon && citizen.Arrows >= weapon.Arrows ? weapon : null;

    /// <summary>
    /// Takes the best weapon and armor from the citizen's hands or village storage. A citizen who already holds
    /// a weapon or armor (because storage had no room to take it back) keeps it.
    /// </summary>
    public static void Arm(Citizen citizen, Village village)
    {
        if (citizen.Weapon is null)
        {
            foreach (var weapon in ArmoryCatalog.Weapons)
            {
                var arrows = weapon.Arrows > 0 ? Available(citizen, village, ResourceKind.Arrows) : 0;
                if (Available(citizen, village, weapon.Kind) == 0 || arrows < weapon.Arrows)
                {
                    continue;
                }

                TakeOne(citizen, village, weapon.Kind);
                citizen.Weapon = weapon.Kind;
                if (weapon.Arrows > 0)
                {
                    var quiver = Math.Min(ArmoryCatalog.QuiverSize, arrows);
                    Take(citizen, village, ResourceKind.Arrows, quiver);
                    citizen.Arrows += quiver;
                }

                break;
            }
        }

        if (citizen.Armor is null && ArmoryCatalog.Armors.FirstOrDefault(a => Available(citizen, village, a.Kind) > 0) is { } armor)
        {
            TakeOne(citizen, village, armor.Kind);
            citizen.Armor = armor.Kind;
        }
    }

    /// <summary>Puts weapon, armor and arrows back into storage. What does not fit stays with the citizen.</summary>
    public static void Disarm(Citizen citizen, Village village)
    {
        if (citizen.Weapon is { } weapon && village.Store(weapon, 1) == 1)
        {
            citizen.Weapon = null;
        }

        if (citizen.Armor is { } armor && village.Store(armor, 1) == 1)
        {
            citizen.Armor = null;
        }

        if (citizen.Arrows > 0)
        {
            citizen.Arrows -= village.Store(ResourceKind.Arrows, citizen.Arrows);
        }
    }

    /// <summary>
    /// A beast may jump at a citizen working out in the wild. New villages are spared; see <see cref="PeacefulTime"/>.
    /// Returns null when nothing attacked.
    /// </summary>
    /// <param name="startedAt">When the cycle started: its daylight decides the danger.</param>
    public static FightReport? Ambush(Citizen citizen, Village village, DangerDefinition danger, Random random, DateTimeOffset startedAt, DateTimeOffset now)
    {
        if (IsPeaceful(village, startedAt) || danger.Enemies.Count == 0)
        {
            return null;
        }

        var chance = danger.Chance * (DayCycle.IsNight(startedAt) ? NightAmbushMultiplier : 1);
        if (random.NextDouble() >= chance)
        {
            return null;
        }

        var enemies = danger.Enemies.Select(EnemyCatalog.Get).ToList();
        var roll = random.Next(enemies.Sum(e => Math.Max(1, e.AmbushWeight)));
        var enemy = enemies[^1];
        foreach (var candidate in enemies)
        {
            var weight = Math.Max(1, candidate.AmbushWeight);
            if (roll < weight)
            {
                enemy = candidate;
                break;
            }

            roll -= weight;
        }

        return Fight(citizen, enemy, random, now);
    }

    /// <summary>One citizen against one enemy. Trains Guarding; the loot is for the caller to store.</summary>
    public static FightReport Fight(Citizen citizen, EnemyDefinition enemy, Random random, DateTimeOffset now)
    {
        var power = Power(citizen);
        var won = random.NextDouble() < power / (power + enemy.Power);
        var weapon = UsableWeapon(citizen)?.Kind;
        var armor = citizen.Armor;
        var injured = Wound(citizen, won ? WonInjuryChance : 1, enemy, random, now);
        var broken = Wear(citizen, random);
        SkillRules.AddExperience(citizen, Skill.Guarding, won ? FightExperience : FightExperience / 2);
        return new FightReport(enemy.Kind, won, injured, injured ? citizen.InjuredUntil : null, power, enemy.Power, weapon, armor,
            won ? enemy.Loot : [], broken);
    }

    /// <summary>Wounds the citizen with this chance, unless their armor stops the blow. Returns true if wounded.</summary>
    public static bool Wound(Citizen citizen, double chance, EnemyDefinition enemy, Random random, DateTimeOffset now)
    {
        var protection = ArmoryCatalog.Armor(citizen.Armor)?.Protection ?? 0;
        if (random.NextDouble() >= chance * (1 - protection))
        {
            return false;
        }

        Injure(citizen, TimeSpan.FromMinutes(enemy.InjuryMinutes), now);
        return true;
    }

    public static void Injure(Citizen citizen, TimeSpan duration, DateTimeOffset now)
    {
        var until = now + duration;
        if (citizen.InjuredUntil is not { } current || current < until)
        {
            citizen.InjuredUntil = until;
        }
    }

    /// <summary>A fight wears weapons down: each may break, and a bow shoots its arrows. Returns what broke.</summary>
    public static IReadOnlyList<ResourceKind> Wear(Citizen citizen, Random random)
    {
        var broken = new List<ResourceKind>();
        if (UsableWeapon(citizen) is { } weapon)
        {
            citizen.Arrows -= weapon.Arrows;
            if (random.NextDouble() < weapon.BreakChance)
            {
                broken.Add(weapon.Kind);
                citizen.Weapon = null;
            }
        }

        if (ArmoryCatalog.Armor(citizen.Armor) is { } armor && random.NextDouble() < armor.BreakChance)
        {
            broken.Add(armor.Kind);
            citizen.Armor = null;
        }

        return broken;
    }

    /// <summary>A remedy closes the wound at once. It is taken from the citizen's hands first, then from storage.</summary>
    public static void Heal(Citizen citizen, Village village, DateTimeOffset now)
    {
        if (!IsInjured(citizen, now))
        {
            throw new GameRuleException("heal.notInjured", "You are not wounded.");
        }

        if (Available(citizen, village, ResourceKind.Remedy) == 0)
        {
            throw new GameRuleException("heal.none", "There is no remedy in storage. A healer must make some first.");
        }

        TakeOne(citizen, village, ResourceKind.Remedy);
        citizen.InjuredUntil = null;
    }

    /// <summary>Damaged buildings work slower: down to half speed at <see cref="MaximumDamage"/>.</summary>
    public static double BuildingMultiplier(Village village, TaskDefinition task) =>
        task.RequiredBuilding is { } kind && village.Structures.FirstOrDefault(s => s.Kind == kind) is { } building
            ? 1 - building.Damage / (2.0 * MaximumDamage)
            : 1;

    /// <summary>Every finished building: plot and damage.</summary>
    public static IEnumerable<(string PlotId, int Damage)> Buildings(Village village) =>
        village.Structures.Select(s => (s.PlotId, s.Damage)).Concat(village.Storages.Select(s => (s.PlotId, s.Damage)));

    /// <summary>The building most in need of repair, or null when none is damaged.</summary>
    public static string? MostDamaged(Village village) =>
        Buildings(village).Where(b => b.Damage > 0).OrderByDescending(b => b.Damage).Select(b => b.PlotId).FirstOrDefault();

    /// <summary>Adds damage to a building, up to <see cref="MaximumDamage"/>. Returns the damage done.</summary>
    public static int Damage(Village village, string plotId, int amount)
    {
        var current = DamageOf(village, plotId);
        var added = Math.Clamp(amount, 0, MaximumDamage - current);
        SetDamage(village, plotId, current + added);
        return added;
    }

    /// <summary>Mends the most damaged building. Returns the damage repaired.</summary>
    public static int Repair(Village village, int amount)
    {
        if (MostDamaged(village) is not { } plotId)
        {
            return 0;
        }

        var current = DamageOf(village, plotId);
        var repaired = Math.Clamp(amount, 0, current);
        SetDamage(village, plotId, current - repaired);
        return repaired;
    }

    public static int DamageOf(Village village, string plotId) =>
        village.Structures.FirstOrDefault(s => s.PlotId == plotId)?.Damage
        ?? village.Storages.FirstOrDefault(s => s.PlotId == plotId)?.Damage
        ?? 0;

    private static void SetDamage(Village village, string plotId, int damage)
    {
        if (village.Structures.FirstOrDefault(s => s.PlotId == plotId) is { } structure)
        {
            structure.Damage = damage;
        }
        else if (village.Storages.FirstOrDefault(s => s.PlotId == plotId) is { } storage)
        {
            storage.Damage = damage;
        }
    }

    private static int Available(Citizen citizen, Village village, ResourceKind kind) => citizen.Carried(kind) + village.Stock(kind);

    private static void TakeOne(Citizen citizen, Village village, ResourceKind kind) => Take(citizen, village, kind, 1);

    private static void Take(Citizen citizen, Village village, ResourceKind kind, int amount)
    {
        var fromHands = citizen.TakeFromLoad(kind, amount);
        village.Take(kind, amount - fromHands);
    }
}
