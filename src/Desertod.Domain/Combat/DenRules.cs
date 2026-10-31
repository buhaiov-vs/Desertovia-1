using Desertod.Domain.Catalog;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Skills;
using Desertod.Domain.Stats;
using Desertod.Domain.World;

namespace Desertod.Domain.Combat;

/// <summary>A citizen at a den: how they come into the fight and what they take out of it.</summary>
public static class DenRules
{
    /// <summary>A citizen who may be woken by a den: up, unhurt, idle and not off to a dungeon.</summary>
    public static bool CanEngage(Citizen citizen, DateTimeOffset now) =>
        RaidSimulation.CanFight(citizen, now) && citizen.CurrentTaskId is null && citizen.RescuePatientId is null;

    /// <summary>Where the citizen stands now.</summary>
    public static TilePoint Position(Citizen citizen, DateTimeOffset now) =>
        citizen.Trip is { } trip && now < trip.ArrivesAt
            ? trip.PositionAt(now)
            : citizen.IdleSpot ?? citizen.Trip?.To ?? WorldMap.Places[WorldMap.Camp];

    /// <summary>Same health as in a village raid; the blow and the armor as the citizen carries them.</summary>
    public static DenFighter Fighter(Citizen citizen)
    {
        var weapon = CombatRules.UsableWeapon(citizen);
        return new DenFighter(
            citizen.Id,
            32 + citizen.Stats.Endurance * 4,
            CombatRules.Power(citizen),
            DungeonRules.Protection(citizen),
            weapon?.Kind == ResourceKind.Bow ? citizen.Arrows : null);
    }

    /// <summary>
    /// The den woke: the citizen stops where they are. The land walked so far stays seen. Returns where the walk was
    /// going, if it was not over yet.
    /// </summary>
    public static TilePoint? Halt(Citizen citizen, Village village, DateTimeOffset now)
    {
        var here = Position(citizen, now);
        TilePoint? destination = citizen.Trip is { } trip && now < trip.ArrivesAt ? trip.To : null;
        var departure = Exploration.SetOut(village.Explored, citizen.Trip, here, here, now, village.Wilds?.Lookout);
        village.Explored = departure.Explored;
        citizen.Trip = departure.Trip;
        citizen.IdleSpot = here;
        return destination;
    }

    /// <summary>
    /// The citizen is out of the fight. Experience grows Guarding and Strength (Dexterity with a bow), loot stays in
    /// their hands far from camp, spent arrows leave the quiver. A citizen who fell stays wounded where they lie;
    /// anyone else walks on where they were going.
    /// </summary>
    public static void Settle(Citizen citizen, Village village, DenOutcome outcome, DateTimeOffset now)
    {
        if (outcome.Experience > 0)
        {
            var stat = citizen.Weapon == ResourceKind.Bow ? Stat.Dexterity : Stat.Strength;
            citizen.Renown += Progression.AddExperience(citizen.Stats, stat, outcome.Experience).RenownGained;
            SkillRules.AddExperience(citizen, Skill.Guarding, outcome.Experience);
            SkillRules.CatchUp(citizen);
        }

        foreach (var good in outcome.Loot.Where(g => g.Amount > 0))
        {
            citizen.Carry(good.Resource, good.Amount);
        }

        if (outcome.ArrowsSpent > 0)
        {
            citizen.SpendArrows(outcome.ArrowsSpent);
        }

        citizen.Trip = null;
        citizen.IdleSpot = outcome.Position;
        if (outcome.KnockedOut)
        {
            CombatRules.Injure(citizen, TimeSpan.FromMinutes(outcome.InjuryMinutes), now, rewardEligible: true);
            citizen.Spot = outcome.Position;
            return;
        }

        if (outcome.Destination is { } destination && WorldMap.Land.Contains(destination.ToTile()))
        {
            citizen.SetOut(destination, village, now);
            citizen.IdleSpot = destination;
        }
    }
}
