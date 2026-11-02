using Desertod.Domain.Buildings;
using Desertod.Domain.Entities;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class RiverBridgeTests
{
    [Fact]
    public void RiverBridge_FitsOnlyAcrossExactlyThreeTilesOfRiver()
    {
        var village = BridgeVillage(out var x, out var y, out var plot);
        var seed = Wilderness.Seed(village.Id.ToString());
        var area = WorldMap.Footprint(plot)!.Value;
        var northSouth = area.Height == WorldMap.BridgeLength;

        Assert.Equal(northSouth ? (3, 5) : (5, 3), (area.Width, area.Height));
        for (var across = 0; across < 3; across++)
        {
            for (var along = 0; along < 5; along++)
            {
                var (tx, ty) = northSouth ? (x + across, y + along) : (x + along, y + across);
                var expected = along is 0 or 4 ? FarWilds.Kind.Open : FarWilds.Kind.Water;
                Assert.Equal(expected, FarWilds.At(tx, ty, seed));
            }
        }

        var (dryX, dryY) = DryLand(seed);
        Assert.Null(Wilderness.RiverBridgePlot(village, dryX, dryY));
    }

    [Fact]
    public void RiverBridge_IsPlacedOverTheRiver_WithItsBuildersOnTheBankNearerTheCamp()
    {
        var village = BridgeVillage(out var x, out var y, out var plot);
        var seed = Wilderness.Seed(village.Id.ToString());
        var (dryX, dryY) = DryLand(seed);
        See(village, WorldMap.Footprint(WorldMap.PlotAt(dryX, dryY))!.Value);

        var site = ConstructionRules.Place(village, BuildingKind.RiverBridge, x, y, Now);
        var yard = WorldMap.Yard(site.PlotId);
        var area = WorldMap.Footprint(site.PlotId)!.Value;
        var camp = WorldMap.Places[WorldMap.Camp];
        var ends = area.Height == WorldMap.BridgeLength
            ? new[] { new TilePoint(area.X + 1.5, area.Y + 0.5), new TilePoint(area.X + 1.5, area.Y + 4.5) }
            : [new TilePoint(area.X + 0.5, area.Y + 1.5), new TilePoint(area.X + 4.5, area.Y + 1.5)];

        Assert.Equal(plot, site.PlotId);
        Assert.Equal(FarWilds.Kind.Open, FarWilds.At((int)yard.X, (int)yard.Y, seed));
        Assert.Equal(ends.MinBy(end => end.DistanceTo(camp)), yard);
        Assert.Equal("building.bridgeNeedsRiver", Assert.Throws<GameRuleException>(() =>
            ConstructionRules.Place(village, BuildingKind.RiverBridge, dryX, dryY, Now)).Code);
    }

    [Fact]
    public void FinishedRiverBridge_CarriesWalkersOverTheRiver()
    {
        var village = BridgeVillage(out _, out _, out var plot);
        var area = WorldMap.Footprint(plot)!.Value;
        var northSouth = area.Height == WorldMap.BridgeLength;
        var from = northSouth ? new TilePoint(area.X + 1.5, area.Y + 0.5) : new TilePoint(area.X + 0.5, area.Y + 1.5);
        var to = northSouth ? new TilePoint(area.X + 1.5, area.Y + 4.5) : new TilePoint(area.X + 4.5, area.Y + 1.5);

        var around = OverlandPath.Find(village, from, to);
        village.Structures.Add(new Structure { Kind = BuildingKind.RiverBridge, PlotId = plot, BuiltAt = Now });
        var over = OverlandPath.Find(village, from, to);

        Assert.True(around is null || around.Count > 1, "the river already had a way straight over");
        Assert.Equal([to], over);
    }

    private static readonly Guid CampId = new("00000000-0000-7000-8000-000000000001");

    /// <summary>The river bridge spot nearest the fixed camp. Found once: the land of one camp never changes.</summary>
    private static readonly Lazy<(int X, int Y, string Plot)> Spot = new(() => NearestSpot(CampId));

    /// <summary>A fixed camp and the river bridge spot nearest to it; the spot is already seen.</summary>
    private static Village BridgeVillage(out int x, out int y, out string plot)
    {
        var village = NewVillage();
        village.Id = CampId;
        (x, y, plot) = Spot.Value;
        See(village, WorldMap.Footprint(plot)!.Value);
        return village;
    }

    private static (int X, int Y, string Plot) NearestSpot(Guid campId)
    {
        var village = NewVillage();
        village.Id = campId;
        var seed = Wilderness.Seed(village.Id.ToString());
        var camp = WorldMap.Places[WorldMap.Camp];
        var land = WorldMap.Land;
        var best = (X: 0, Y: 0, Plot: (string?)null, Distance: double.MaxValue);
        for (var row = land.Y + 10; row < land.Y + land.Height - 10; row += 2)
        {
            for (var column = land.X + 10; column < land.X + land.Width - 10; column++)
            {
                if (FarWilds.At(column, row, seed) != FarWilds.Kind.Water)
                {
                    continue;
                }

                for (var dy = -4; dy <= 0; dy++)
                {
                    for (var dx = -4; dx <= 0; dx++)
                    {
                        var found = Wilderness.RiverBridgePlot(village, column + dx, row + dy);
                        var distance = new TilePoint(column, row).DistanceTo(camp);
                        if (found is not null && distance < best.Distance)
                        {
                            best = (column + dx, row + dy, found, distance);
                        }
                    }
                }
            }

            if (best.Plot is not null && Math.Abs(row - camp.Y) > best.Distance)
            {
                break;
            }
        }

        return (best.X, best.Y, best.Plot ?? throw new InvalidOperationException("No river bridge spot on this seed."));
    }

    /// <summary>An open tile with open land all around it, far from any river.</summary>
    private static (int X, int Y) DryLand(uint seed)
    {
        for (var x = 400; ; x += 7)
        {
            var dry = true;
            for (var dy = -6; dy <= 10 && dry; dy++)
            {
                for (var dx = -6; dx <= 10 && dry; dx++)
                {
                    dry = FarWilds.At(x + dx, 13 + dy, seed) == FarWilds.Kind.Open;
                }
            }

            if (dry) return (x, 13);
        }
    }

    private static void See(Village village, TileArea area) =>
        village.Explored = Exploration.Reveal(village.Explored, new TilePoint(area.X + area.Width / 2.0, area.Y + area.Height / 2.0), 6);
}
