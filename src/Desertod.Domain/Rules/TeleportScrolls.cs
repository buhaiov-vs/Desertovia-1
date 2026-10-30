using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Combat;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.World;

namespace Desertod.Domain.Rules;

/// <summary>Written by scholars once the village knows waystones; reading one summons the reader to the nearest waystone.</summary>
public static class TeleportScrolls
{
    public const int ReadingIntelligence = 4;

    /// <summary>The plot of the nearest whole waystone the citizen may use: a public one or their own.</summary>
    public static string? Destination(Citizen citizen, Village village, TilePoint from) =>
        village.Structures
            .Where(s => s.Kind == BuildingKind.Waystone && (s.OwnerAccountId is null || s.OwnerAccountId == citizen.AccountId)
                && !s.Decommissioning && s.RuinedAt is null && s.Damage < 100)
            .Select(s => s.PlotId)
            .OrderBy(plot => WorldMap.Yard(plot).DistanceTo(from))
            .ThenBy(plot => plot, StringComparer.Ordinal)
            .FirstOrDefault();

    public static TilePoint Position(Citizen citizen, DateTimeOffset now) =>
        citizen.Trip is { } trip && now < trip.ArrivesAt
            ? trip.PositionAt(now)
            : citizen.IdleSpot ?? WorldMap.Places[WorldMap.Camp];

    /// <summary>Takes one owned scroll first, then from the citizen's hands or the village storage, and moves the reader.</summary>
    public static TilePoint Read(Citizen citizen, Village village, DateTimeOffset now)
    {
        if (citizen.Status == CitizenStatus.Dormant)
            throw new GameRuleException("citizen.dormant", "Dormant citizens cannot work.");
        HospitalRules.RequireReady(citizen, now);
        if (CombatRules.IsInjured(citizen, now))
        {
            throw new GameRuleException("citizen.injured", "You are wounded and cannot walk until the wound heals.",
                new Dictionary<string, object> { ["readyAt"] = citizen.InjuredUntil!.Value });
        }

        if (citizen.ExhaustedUntil > now) throw new GameRuleException("citizen.exhausted", "You are unconscious and recovering.");
        if (citizen.WorkState(now) == WorkState.Working)
            throw new GameRuleException("goto.working", "Finish the current task first.");
        if (citizen.DungeonRunId is not null)
            throw new GameRuleException("dungeon.busy", "You are away in the dungeon.");
        if (citizen.RescuePatientId is not null)
            throw new GameRuleException("teleport.carrying", "Put the wounded down before reading the scroll.");
        if (citizen.Stats.Intelligence < ReadingIntelligence)
        {
            throw new GameRuleException("teleport.intelligence", "You cannot make out the runes on the scroll.",
                new Dictionary<string, object> { ["minimum"] = ReadingIntelligence });
        }

        var from = Position(citizen, now);
        var plot = Destination(citizen, village, from)
            ?? throw new GameRuleException("teleport.noWaystone", "There is no whole waystone to be summoned to.");

        if (PersonalInventory.Amount(citizen, ResourceKind.TeleportScroll) > 0)
            PersonalInventory.Remove(citizen, ResourceKind.TeleportScroll, 1);
        else if (citizen.TakeFromLoad(ResourceKind.TeleportScroll, 1) == 0 && village.Take(ResourceKind.TeleportScroll, 1) == 0)
            throw new GameRuleException("teleport.noScroll", "There is no scroll of teleport in your hands or in storage.");

        if (citizen.Trip is { } walked) village.Explore([walked], now);
        var to = WorldMap.Yard(plot);
        citizen.Trip = null;
        citizen.IdleSpot = to;
        citizen.LastTeleportAt = now;
        village.Explored = Exploration.Reveal(village.Explored, to, Exploration.SightRadius);
        return to;
    }
}
