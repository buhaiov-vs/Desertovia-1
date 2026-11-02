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
    /// <summary>
    /// A citizen who may be woken by a den: up, unhurt, not off to a dungeon and not in the middle of a work cycle.
    /// A finished cycle keeps its task until it is renewed; that citizen walks free, so a den wakes for them too.
    /// </summary>
    public static bool CanEngage(Citizen citizen, DateTimeOffset now) =>
        RaidSimulation.CanFight(citizen, now) && citizen.WorkState(now) != WorkState.Working && citizen.RescuePatientId is null;

    /// <summary>Where the citizen stands now.</summary>
    public static TilePoint Position(Citizen citizen, DateTimeOffset now) =>
        citizen.Trip is { } trip && now < trip.ArrivesAt
            ? trip.PositionAt(now)
            : citizen.IdleSpot ?? citizen.Trip?.To ?? WorldMap.Places[WorldMap.Camp];

    /// <summary>Same health pool as in a village raid; HP may still be regenerating from the last den fight.</summary>
    public static DenFighter Fighter(Citizen citizen, DateTimeOffset now)
    {
        var weapon = CombatRules.UsableWeapon(citizen);
        var maximum = citizen.MaximumCombatHealth;
        return new DenFighter(
            citizen.Id,
            citizen.CurrentCombatHealth(now),
            maximum,
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
        var departure = Exploration.SetOut(village.Explored, citizen.Trip, here, here, now);
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

        citizen.SetCombatHealth(outcome.Health, now);
        citizen.Trip = null;
        if (outcome.KnockedOut)
        {
            // A wound remembers the previous spot. Put the body back on the tile where they fell, and do not
            // start the walk they had been on — that walk is often the way back to the village.
            CombatRules.Injure(citizen, TimeSpan.FromMinutes(outcome.InjuryMinutes), now, rewardEligible: true);
            citizen.Trip = null;
            citizen.IdleSpot = outcome.Position;
            citizen.Spot = outcome.Position;
            return;
        }

        citizen.IdleSpot = outcome.Position;

        if (outcome.Destination is { } destination && WorldMap.Land.Contains(destination.ToTile()))
        {
            citizen.SetOut(destination, village, now);
            citizen.IdleSpot = destination;
        }
    }
}
