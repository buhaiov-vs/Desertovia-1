using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class TeleportScrollTests
{
    private static (Village Village, Citizen Citizen) Reader(int intelligence = TeleportScrolls.ReadingIntelligence)
    {
        var village = NewVillage();
        village.Structures.Add(new() { Kind = BuildingKind.Waystone, PlotId = "30,30" });
        var citizen = NewCitizen(village, (Stat.Intelligence, intelligence));
        return (village, citizen);
    }

    [Fact]
    public void Reading_summons_the_citizen_to_the_waystone_and_uses_the_scroll()
    {
        var (village, citizen) = Reader();
        village.Store(ResourceKind.TeleportScroll, 1);
        citizen.Trip = Trip.Start(WorldMap.Places[WorldMap.Camp], WorldMap.Yard("10,10"), Now.AddSeconds(-5));

        var to = TeleportScrolls.Read(citizen, village, Now);

        Assert.Equal(WorldMap.Yard("30,30"), to);
        Assert.Equal(to, citizen.IdleSpot);
        Assert.Null(citizen.Trip);
        Assert.Equal(Now, citizen.LastTeleportAt);
        Assert.Equal(0, village.Stock(ResourceKind.TeleportScroll));
        Assert.True(village.IsExplored(new TileArea((int)to.X, (int)to.Y, 1, 1)));
    }

    [Fact]
    public void Owned_scroll_is_read_before_the_common_supply()
    {
        var (village, citizen) = Reader();
        village.Store(ResourceKind.TeleportScroll, 1);
        PersonalInventory.Add(citizen, ResourceKind.TeleportScroll, 1, 120);

        TeleportScrolls.Read(citizen, village, Now);

        Assert.Equal(0, PersonalInventory.Amount(citizen, ResourceKind.TeleportScroll));
        Assert.Equal(1, village.Stock(ResourceKind.TeleportScroll));
    }

    [Fact]
    public void The_nearest_usable_waystone_is_chosen()
    {
        var (village, citizen) = Reader();
        village.Structures.Add(new() { Kind = BuildingKind.Waystone, PlotId = "60,60", OwnerAccountId = Guid.NewGuid() });
        village.Structures.Add(new() { Kind = BuildingKind.Waystone, PlotId = "50,50", Damage = 100 });
        village.Structures.Add(new() { Kind = BuildingKind.Waystone, PlotId = "45,45", OwnerAccountId = citizen.AccountId });
        village.Store(ResourceKind.TeleportScroll, 1);
        citizen.IdleSpot = WorldMap.Yard("55,55");

        Assert.Equal(WorldMap.Yard("45,45"), TeleportScrolls.Read(citizen, village, Now));
    }

    [Fact]
    public void A_dull_reader_cannot_make_out_the_runes()
    {
        var (village, citizen) = Reader(TeleportScrolls.ReadingIntelligence - 1);
        village.Store(ResourceKind.TeleportScroll, 1);

        var error = Assert.Throws<GameRuleException>(() => TeleportScrolls.Read(citizen, village, Now));

        Assert.Equal("teleport.intelligence", error.Code);
        Assert.Equal(1, village.Stock(ResourceKind.TeleportScroll));
        Assert.Null(citizen.LastTeleportAt);
    }

    [Fact]
    public void Without_a_whole_waystone_the_scroll_is_kept()
    {
        var village = NewVillage();
        village.Structures.Add(new() { Kind = BuildingKind.Waystone, PlotId = "30,30", RuinedAt = Now });
        village.Store(ResourceKind.TeleportScroll, 1);
        var citizen = NewCitizen(village, (Stat.Intelligence, TeleportScrolls.ReadingIntelligence));

        Assert.Equal("teleport.noWaystone", Assert.Throws<GameRuleException>(() => TeleportScrolls.Read(citizen, village, Now)).Code);
        Assert.Equal(1, village.Stock(ResourceKind.TeleportScroll));
    }

    [Fact]
    public void A_scroll_is_needed()
    {
        var (village, citizen) = Reader();

        Assert.Equal("teleport.noScroll", Assert.Throws<GameRuleException>(() => TeleportScrolls.Read(citizen, village, Now)).Code);
        Assert.Null(citizen.LastTeleportAt);
    }

    [Fact]
    public void Busy_readers_are_refused()
    {
        var (village, citizen) = Reader();
        village.Store(ResourceKind.TeleportScroll, 1);
        citizen.DungeonRunId = Guid.NewGuid();
        Assert.Equal("dungeon.busy", Assert.Throws<GameRuleException>(() => TeleportScrolls.Read(citizen, village, Now)).Code);

        citizen.DungeonRunId = null;
        citizen.RescuePatientId = Guid.NewGuid();
        Assert.Equal("teleport.carrying", Assert.Throws<GameRuleException>(() => TeleportScrolls.Read(citizen, village, Now)).Code);

        citizen.RescuePatientId = null;
        citizen.ExhaustedUntil = Now.AddHours(1);
        Assert.Equal("citizen.exhausted", Assert.Throws<GameRuleException>(() => TeleportScrolls.Read(citizen, village, Now)).Code);
        Assert.Equal(1, village.Stock(ResourceKind.TeleportScroll));
    }

    [Fact]
    public void Scrolls_need_waystones_moonstone_and_a_journeyman_scholar()
    {
        var write = Task("write-teleport-scroll");
        Assert.Equal(TechnologyCatalog.Waystones, write.RequiredTechnology);
        Assert.Equal("scholar", write.RoleId);
        Assert.Contains(write.Inputs, i => i.Resource == ResourceKind.Moonstone);
        Assert.Contains(write.Inputs, i => i.Resource == ResourceKind.Parchment);
        Assert.Equal(TechnologyCatalog.Waystones, Task("chip-moonstone").RequiredTechnology);
        Assert.Equal(Wilderness.Cave, Task("chip-moonstone").Workplace);
        Assert.Equal(TechnologyCatalog.Waystones, BuildingCatalog.Get(BuildingKind.Waystone).Technology);
        Assert.Contains(TechnologyCatalog.Writing, TechnologyCatalog.Get(TechnologyCatalog.Waystones).Requires);
    }
}
