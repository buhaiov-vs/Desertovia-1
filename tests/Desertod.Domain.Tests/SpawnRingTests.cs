using System.Text.Json;
using Desertod.Domain.Combat;
using Desertod.Domain.Weather;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class SpawnRingTests
{
    [Fact]
    public void Rings_KeepEightPointsUntilTheEdge_AndFourCornersAfterIt()
    {
        var points = SpawnRings.Points(7);
        Assert.Equal(30, SpawnRings.Levels.Count);
        Assert.Equal(8, points.Count(point => point.Level == 1));
        Assert.Equal(8, points.Count(point => point.Level == 22));
        Assert.Equal(4, points.Count(point => point.Level == 23));
        Assert.Equal(4, points.Count(point => point.Level == 30));
        Assert.All(points, point =>
        {
            Assert.True(WorldMap.Land.Contains(new TileArea(point.X, point.Y, 1, 1)));
            Assert.False(FarGround.Water(point.X, point.Y, 7));
            Assert.True(point.Center.DistanceTo(WorldMap.Places[WorldMap.Camp]) + 1 >= SpawnRings.Levels[point.Level - 1].Floor);
        });
        Assert.Equal(points.Select(point => point.Id), SpawnRings.Points(7).Select(point => point.Id));
    }

    public sealed class RingCase
    {
        public uint Seed { get; set; }
        public string[] Points { get; set; } = [];
    }

    public static IEnumerable<object[]> RingCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "spawn-rings.json");
        return JsonSerializer.Deserialize<RingCase[]>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Select(sample => new object[] { sample });
    }

    [Theory]
    [MemberData(nameof(RingCases))]
    public void Dens_MatchTheOnesTheBrowserDraws(RingCase sample)
    {
        var points = SpawnRings.Points(sample.Seed).Select(point => $"{point.Id} {point.X},{point.Y}");
        Assert.Equal(sample.Points, points);
    }

    [Fact]
    public void Night_SendsEveryLivingAllowedPoint_OnTheNearestFog()
    {
        var points = SpawnRings.Points(1);
        var camp = WorldMap.Places[WorldMap.Camp];
        var none = SpawnRings.Senders(points, (_, _) => false);
        Assert.Empty(none);

        var wolf = points.First(point => point.Kind == EnemyKind.Wolf);
        var senders = SpawnRings.Senders(points, (x, y) => x == wolf.X && y == wolf.Y);
        Assert.Contains(senders, point => point.Kind == EnemyKind.Wolf);
        Assert.Contains(senders, point => point.Kind == EnemyKind.Boar);
        Assert.DoesNotContain(senders, point => point.Level == 7);

        var gone = SpawnRings.Senders(points, (x, y) => x == wolf.X && y == wolf.Y, new HashSet<string> { wolf.Id });
        Assert.DoesNotContain(gone, point => point.Id == wolf.Id);

        var atThePoint = SpawnRings.Appearance(camp, wolf.Center, (_, _) => true);
        Assert.Equal(wolf.Center, atThePoint);
        var inTheFog = SpawnRings.Appearance(camp, wolf.Center, (_, _) => false);
        Assert.True(inTheFog.DistanceTo(camp) < wolf.Center.DistanceTo(camp));
        Assert.Equal(1, SpawnRings.Bodies(SpawnRings.Levels[4]));
        Assert.Equal(0, SpawnRings.Bodies(SpawnRings.Levels[0]));
    }

    [Fact]
    public void EightCitizens_HoldADeadDen_AndALichCannotBeRenewed()
    {
        var point = SpawnRings.Points(1).First(item => item.Level == 1);
        Assert.Equal(8, SpawnRings.SpotsAround(point).Count);
        var now = DateTimeOffset.UnixEpoch;
        var present = Enumerable.Range(0, 8).Select(spot => new SpotPresence(spot, Guid.NewGuid(), now)).ToList();
        Assert.False(SpawnRings.Falls(true, present, now.AddSeconds(2)));
        Assert.True(SpawnRings.Falls(true, present, now.AddSeconds(3)));
        Assert.False(SpawnRings.Falls(false, present, now.AddSeconds(3)));
        Assert.False(SpawnRings.Falls(true, present.Take(7).ToList(), now.AddSeconds(3)));

        Assert.Equal("The renewal resource is not set.", SpawnRings.RenewalBlock(29));
        Assert.Equal("A lich point cannot be renewed.", SpawnRings.RenewalBlock(30));
        Assert.Empty(SpawnRings.RenewalGoods);
        Assert.Null(SpawnRings.CavePlace);
        Assert.Empty(SpawnRings.CaveFoes(SpawnRings.Points(1), new HashSet<string>()));
        var destroyed = new HashSet<string> { point.Id };
        Assert.Equal([point.Name], SpawnRings.CaveFoes(SpawnRings.Points(1), destroyed));
    }

    [Fact]
    public void PlanNight_WarnsAhead_WhenACatalogFoeIsAllowed()
    {
        var village = NewVillage();
        village.CreatedAt = Now - CombatRules.PeacefulTime - TimeSpan.FromHours(1);
        var period = DayCycle.PeriodOf(Now);
        var night = DayCycle.IsNight(period) ? period : period + 1;
        var dusk = DayCycle.PeriodStart(night);
        var seed = Wilderness.Seed(village.Id.ToString());
        var wolf = SpawnRings.Points(seed).First(point => point.Kind == EnemyKind.Wolf);
        var trip = Trip.Start(wolf.Center, wolf.Center, dusk);
        village.Explored = Exploration.Reveal(Exploration.Camp(), trip, trip.ArrivesAt);

        var raid = RaidRules.PlanNight(village, 4, new Random(1), dusk);

        Assert.NotNull(raid);
        Assert.Equal(raid.StrikesAt - RaidRules.Warning, raid.AnnouncedAt);
        Assert.Contains(village.Raids, item => item.Enemy == EnemyKind.Wolf && item.Source == RaidSource.Night);
        Assert.All(village.Raids, item => Assert.NotEmpty(item.ApproachPath));
    }
}
