using System.Text.Json;
using Desertod.Domain.Combat;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public class HauntTests
{
    public sealed class HauntCase
    {
        public uint Seed { get; set; }
        public string[] Foes { get; set; } = [];
    }

    public static IEnumerable<object[]> HauntCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "haunts.json");
        return JsonSerializer.Deserialize<HauntCase[]>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Select(sample => new object[] { sample });
    }

    [Theory]
    [MemberData(nameof(HauntCases))]
    public void Foes_MatchTheOnesTheBrowserDraws(HauntCase sample)
    {
        var dens = SpawnRings.Points(sample.Seed);
        var foes = sample.Foes.Select(line =>
        {
            var cell = line.Split(' ')[0].Split(':');
            var (x, y) = (int.Parse(cell[1]), int.Parse(cell[2]));
            return Haunts.At(sample.Seed, x, y, dens) is { } foe ? $"{foe.Id} {foe.Level} {foe.X},{foe.Y}" : $"h:{x}:{y} none";
        });
        Assert.Equal(sample.Foes, foes);
    }

    public sealed record WalkSpot(double Seconds, double X, double Y);

    public sealed record WalkCase(uint Seed, int CellX, int CellY, WalkSpot[] Walks);

    public static IEnumerable<object[]> WalkCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "haunt-walks.json");
        return JsonSerializer.Deserialize<WalkCase[]>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Select(sample => new object[] { sample });
    }

    [Theory]
    [MemberData(nameof(WalkCases))]
    public void Wander_MatchesTheOneTheBrowserDraws(WalkCase sample)
    {
        var foe = Haunts.At(sample.Seed, sample.CellX, sample.CellY, SpawnRings.Points(sample.Seed))!;
        foreach (var spot in sample.Walks)
        {
            var here = Haunts.PositionAt(sample.Seed, foe, DateTimeOffset.UnixEpoch.AddMilliseconds(spot.Seconds * 1000));
            Assert.Equal(spot.X, here.X, 6);
            Assert.Equal(spot.Y, here.Y, 6);
        }
    }

    [Fact]
    public void NoFoe_StandsOrWandersOnWaterMountainsOrThickets()
    {
        var dens = SpawnRings.Points(7);
        var reach = (int)Haunts.WanderRadius + 1;
        var foes = 0;
        for (var cellY = -60; cellY <= 60; cellY += 2)
        {
            for (var cellX = -60; cellX <= 60; cellX += 2)
            {
                if (Haunts.At(7, cellX, cellY, dens) is not { } foe)
                {
                    continue;
                }

                foes++;
                for (var dy = -reach; dy <= reach; dy++)
                {
                    for (var dx = -reach; dx <= reach; dx++)
                    {
                        if (dx * dx + dy * dy <= reach * reach)
                        {
                            Assert.False(FarWilds.Taken(foe.X + dx, foe.Y + dy, 7), $"{foe.Id} near {foe.X + dx},{foe.Y + dy}");
                        }
                    }
                }
            }
        }

        Assert.True(foes > 100, $"foes {foes}");
        Assert.All(dens, den => Assert.False(FarWilds.Taken(den.X, den.Y, 7), den.Id));
    }

    [Fact]
    public void AFoe_IsRarer_TheFurtherAboveItsBandItIs()
    {
        var counts = Enumerable.Range(0, 10_000)
            .Select(step => Haunts.LevelOf(10, (step + 0.5) / 10_000))
            .GroupBy(level => level)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal([500, 6_500, 1_200, 900, 600, 300], new[] { 9, 10, 11, 12, 13, 14 }.Select(level => counts[level]));
        Assert.Equal(SpawnRings.Levels.Count, Haunts.LevelOf(SpawnRings.Levels.Count - 1, 0.06));
        Assert.Equal(1, Haunts.LevelOf(1, 0));
    }

    [Fact]
    public void TwoInThreeFoes_AreOfTheirBand_AndNoneStandsNearTheValeOrADen()
    {
        var dens = SpawnRings.Points(7);
        var camp = WorldMap.Places[WorldMap.Camp];
        var (own, off) = (0, 0);
        for (var cellY = -150; cellY <= 150; cellY++)
        {
            for (var cellX = -150; cellX <= 150; cellX += 3)
            {
                if (Haunts.At(7, cellX, cellY, dens) is not { } foe)
                {
                    continue;
                }

                var distance = foe.Center.DistanceTo(camp);
                var band = Math.Max(1, SpawnRings.Levels.Count(level => level.Floor <= distance));
                Assert.InRange(foe.Level, band - 1, band + 4);
                Assert.True(Haunts.FromVale(foe.Center) >= Haunts.ValeClearance);
                Assert.All(dens, den => Assert.True(den.Center.DistanceTo(foe.Center) >= Haunts.DenClearance));
                Assert.True(foe.Roams);
                if (foe.Level == band) own++;
                else off++;
            }
        }

        Assert.InRange((double)off / (own + off), 0.3, 0.4);
    }

    [Fact]
    public void LevelOneFoes_RoamAroundTheVale_InsideTheFirstRing_ButNeverWanderIntoIt()
    {
        var dens = SpawnRings.Points(1);
        var camp = WorldMap.Places[WorldMap.Camp];
        var inner = new List<SpawnPoint>();
        for (var cellY = -15; cellY <= 15; cellY++)
        {
            for (var cellX = -15; cellX <= 15; cellX++)
            {
                if (Haunts.At(1, cellX, cellY, dens) is { } foe && foe.Center.DistanceTo(camp) < SpawnRings.Levels[0].Floor)
                {
                    inner.Add(foe);
                }
            }
        }

        Assert.True(inner.Count > 100, $"{inner.Count} foes inside the first ring");
        Assert.All(inner, foe => Assert.InRange(foe.Level, 1, 5));
        Assert.True(inner.Count(foe => foe.Level == 1) > inner.Count * 0.6);
        Assert.Contains(inner, foe => Haunts.FromVale(foe.Center) < 40);
        var start = DateTimeOffset.UnixEpoch.AddDays(20_000);
        Assert.All(inner, foe => Assert.All(Enumerable.Range(0, 240), step =>
            Assert.True(Haunts.FromVale(Haunts.PositionAt(1, foe, start.AddSeconds(step * 5))) > DenGuards.AggroRange)));
        Assert.All(dens, den => Assert.True(den.Center.DistanceTo(camp) >= SpawnRings.Levels[0].Floor));
    }

    [Fact]
    public void AFoe_FightsAsOneProtectorOfItsLevel_OnItsOwnTile()
    {
        var dens = SpawnRings.Points(1);
        var foe = Enumerable.Range(10, 40).Select(cell => Haunts.At(1, cell, 0, dens)).First(foe => foe is not null)!;
        var now = DateTimeOffset.UnixEpoch.AddDays(20_000).AddSeconds(7);
        var where = Haunts.PositionAt(1, foe, now);
        var fight = new DenFight(foe, (_, _) => true, where);

        var protector = Assert.Single(fight.Protectors);
        Assert.Equal(where, protector.Home);
        Assert.Equal(where, fight.Anchor);
        Assert.Equal(DenGuards.For(SpawnRings.Levels[foe.Level - 1]).Health, protector.Health);
        Assert.Equal(Haunts.Claim / 2.0, fight.Leash);
        Assert.True(fight.Wakes(new TilePoint(where.X + DenGuards.AggroRange - 0.5, where.Y)));
        Assert.False(fight.Wakes(new TilePoint(where.X + DenGuards.AggroRange + 0.5, where.Y)));
        Assert.Contains(Haunts.Near(1, where, DenGuards.AggroRange, dens, now), near => near.Id == foe.Id);
    }

    [Fact]
    public void AFoe_WandersNearItsTile_WithoutJumping()
    {
        var dens = SpawnRings.Points(1);
        var foe = Enumerable.Range(10, 40).Select(cell => Haunts.At(1, cell, 0, dens)).First(foe => foe is not null)!;
        var start = DateTimeOffset.UnixEpoch.AddDays(20_000);
        var seen = new HashSet<(int, int)>();
        var last = Haunts.PositionAt(1, foe, start);
        for (var step = 1; step <= 600; step++)
        {
            var here = Haunts.PositionAt(1, foe, start.AddSeconds(step));
            Assert.True(here.DistanceTo(foe.Center) <= Haunts.WanderRadius + 1e-9);
            // A leg is at most two radii long and is walked within three quarters of the leg.
            Assert.True(here.DistanceTo(last) <= 2 * Haunts.WanderRadius / (Haunts.LegSeconds * 0.75) + 1e-9);
            seen.Add(((int)Math.Floor(here.X), (int)Math.Floor(here.Y)));
            last = here;
        }

        Assert.True(seen.Count > 10);
    }
}
