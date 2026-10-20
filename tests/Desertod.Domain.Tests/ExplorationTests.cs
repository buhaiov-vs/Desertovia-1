using Desertod.Domain.Buildings;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class ExplorationTests
{
    private static bool Seen(byte[] map, TilePoint point) => Exploration.IsExplored(map, (int)Math.Floor(point.X), (int)Math.Floor(point.Y));

    [Fact]
    public void NewTribe_KnowsTheCampLand_ButNotTheWildBeyond()
    {
        var village = NewVillage();

        Assert.True(Seen(village.Explored, WorldMap.Places[WorldMap.Camp]));
        Assert.True(Seen(village.Explored, WorldMap.Places["stones"]));
        Assert.True(Seen(village.Explored, WorldMap.Places[WorldMap.Ford]));
        Assert.False(Seen(village.Explored, WorldMap.Places[WorldMap.GoldVein]));
        Assert.False(Seen(village.Explored, WorldMap.Places["deer-woods"]));
    }

    [Fact]
    public void WalkedLand_IsSeenOnlyOnceTheWalkerGetsThere()
    {
        var village = NewVillage();
        var far = new TilePoint(-30, -20);

        village.TsarGoTo(far, Now);
        var trip = village.TsarTrip!;

        Assert.False(Seen(Exploration.Seen(village.Explored, [trip], Now), far));
        var halfway = Now + (trip.ArrivesAt - Now) / 2;
        Assert.False(Seen(Exploration.Seen(village.Explored, [trip], halfway), far));
        Assert.True(Seen(Exploration.Seen(village.Explored, [trip], halfway), trip.PositionAt(halfway)));
        Assert.True(Seen(Exploration.Seen(village.Explored, [trip], trip.ArrivesAt), far));
    }

    [Fact]
    public void TheNextWalk_KeepsWhatTheLastOneSaw()
    {
        var village = NewVillage();
        var far = new TilePoint(-30, -20);
        village.TsarGoTo(far, Now);
        var arrived = village.TsarTrip!.ArrivesAt;

        village.TsarGoTo(WorldMap.Places[WorldMap.Camp], arrived);

        Assert.True(Seen(village.Explored, far));
        Assert.Equal(far, village.TsarTrip!.From);
    }

    [Fact]
    public void Blueprint_NeedsSeenLand()
    {
        var village = NewVillage();

        Assert.Equal("building.unexplored",
            Assert.Throws<GameRuleException>(() => ConstructionRules.Place(village, BuildingKind.WoodPile, 0, 0, Now)).Code);

        village.TsarGoTo(new TilePoint(1.5, 2), Now);
        village.Explore([], village.TsarTrip!.ArrivesAt);

        Assert.Equal(BuildingKind.WoodPile, ConstructionRules.Place(village, BuildingKind.WoodPile, 0, 0, Now).Kind);
    }

    [Fact]
    public void CitizenGoingToWork_SeesTheWorkplaceOnArrival()
    {
        var village = NewVillage();
        var citizen = NewCitizen(village);
        var woods = WorldMap.Places["deer-woods"];

        citizen.GoTo(woods, village, Now);

        Assert.False(Seen(Exploration.Seen(village.Explored, [citizen.Trip!], Now), woods));
        Assert.True(Seen(Exploration.Seen(village.Explored, [citizen.Trip!], citizen.Trip!.ArrivesAt), woods));
    }

    [Fact]
    public void LegacyBitmap_BecomesChunks_WithoutLosingTiles()
    {
        var legacy = Enumerable.Repeat((byte)255, Exploration.LegacySize).ToArray();

        var chunks = Exploration.ImportLegacy(legacy);

        Assert.True(Exploration.IsChunkFormat(chunks));
        Assert.True(Exploration.IsExplored(chunks, WorldMap.Land.X, WorldMap.Land.Y));
        Assert.True(Exploration.IsExplored(chunks, WorldMap.Land.X + WorldMap.Land.Width - 1, WorldMap.Land.Y + WorldMap.Land.Height - 1));
        Assert.InRange(Exploration.EntireLand().Length, 1, 200);
        Assert.True(Exploration.Camp().Length < Exploration.LegacySize);
        Assert.Equal(legacy, Exploration.LegacyBitmap(chunks));
    }

    [Fact]
    public void WalkedLand_MatchesADenseScan_AndStaysSmallerThanTheOldBitmap()
    {
        var from = WorldMap.Places[WorldMap.Camp];
        var to = new TilePoint(WorldMap.Land.X + 1.5, WorldMap.Land.Y + WorldMap.Land.Height - 1.5);
        var trip = Trip.Start(from, to, Now);
        var revealed = Exploration.Reveal(Exploration.Camp(), trip, trip.ArrivesAt);

        var land = WorldMap.Land;
        for (var y = land.Y; y < land.Y + land.Height; y++)
        {
            for (var x = land.X; x < land.X + land.Width; x++)
            {
                var onTrip = DistanceToSegment(new TilePoint(x + 0.5, y + 0.5), from, to) <= Exploration.SightRadius;
                var inCamp = InOval(new TilePoint(x + 0.5, y + 0.5), WorldMap.Places[WorldMap.Camp], Exploration.CampRadiusX, Exploration.CampRadiusY);
                Assert.Equal(onTrip || inCamp, Exploration.IsExplored(revealed, x, y));
            }
        }

        Assert.True(revealed.Length < Exploration.LegacySize);
    }

    private static bool InOval(TilePoint point, TilePoint center, double radiusX, double radiusY)
    {
        var dx = (point.X - center.X) / radiusX;
        var dy = (point.Y - center.Y) / radiusY;
        return dx * dx + dy * dy <= 1;
    }

    private static double DistanceToSegment(TilePoint point, TilePoint from, TilePoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var lengthSquared = dx * dx + dy * dy;
        var share = lengthSquared == 0 ? 0 : Math.Clamp(((point.X - from.X) * dx + (point.Y - from.Y) * dy) / lengthSquared, 0, 1);
        return point.DistanceTo(new TilePoint(from.X + dx * share, from.Y + dy * share));
    }
}
