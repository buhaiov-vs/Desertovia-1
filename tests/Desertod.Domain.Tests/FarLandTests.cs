using System.Diagnostics;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public class FarLandTests
{
    [Fact]
    public void Land_IsTheSquareAroundTheCamp_AndTheValeStaysTheOldPicture()
    {
        Assert.Equal(288_000, WorldMap.Land.Width);
        Assert.Equal(288_000, WorldMap.Land.Height);
        Assert.Equal(120, WorldMap.Vale.Width);
        Assert.Equal(90, WorldMap.Vale.Height);
        Assert.Equal(1_350, Exploration.LegacySize);
        Assert.InRange(Exploration.EntireLand().Length, 1, 200);
        Assert.True(WorldMap.Land.Contains(WorldMap.Places[WorldMap.Camp].ToTile()));
        Assert.True(WorldMap.Land.Contains(new TileArea(WorldMap.Land.X, WorldMap.Land.Y, 1, 1)));
        Assert.False(WorldMap.Vale.Contains(new TileArea(WorldMap.Land.X, WorldMap.Land.Y, 1, 1)));
    }

    [Fact]
    public void FarGround_IsDry_AndAQueryStaysCheap()
    {
        const uint seed = 1_234;
        var left = FarGround.RiverLeft(10_000, seed);
        Assert.False(FarGround.Water(left, 10_000, seed));
        Assert.False(FarGround.Water(left + 3, 10_000, seed));
        Assert.False(FarGround.Water(left - 1, 10_000, seed));
        Assert.False(FarGround.Water(left + 4, 10_000, seed));

        var watch = Stopwatch.StartNew();
        var wet = 0;
        for (var i = 0; i < 10_000; i++)
        {
            if (FarGround.Water(i - 5_000, i, seed)) wet++;
        }

        watch.Stop();
        Assert.Equal(0, wet);
        Assert.True(watch.ElapsedMilliseconds < 50, $"10,000 ground queries took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void RevealingAFarChunk_DoesNotStoreTheWholeLand()
    {
        var map = Exploration.Camp();
        var far = new TilePoint(WorldMap.Vale.X - 20.5, WorldMap.Places[WorldMap.Camp].Y);
        var trip = Trip.Start(WorldMap.Places[WorldMap.Camp], far, DateTimeOffset.UnixEpoch);
        var watch = Stopwatch.StartNew();
        var revealed = Exploration.Reveal(map, trip, trip.ArrivesAt);
        watch.Stop();

        Assert.True(Exploration.IsExplored(revealed, (int)far.X, (int)far.Y));
        Assert.False(WorldMap.Vale.Contains(far.ToTile()));
        Assert.True(revealed.Length < 8 * 1024, $"a short far reveal stored {revealed.Length} bytes");
        Assert.True(watch.ElapsedMilliseconds < 50, $"a short far reveal took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Measure_JoinFogAndALongWalk()
    {
        var join = Stopwatch.StartNew();
        var wilds = Wilderness.Generate(Guid.NewGuid());
        var camp = Exploration.Camp();
        join.Stop();

        var fog = Stopwatch.StartNew();
        var near = new TilePoint(WorldMap.Vale.X - 8.5, WorldMap.Places[WorldMap.Camp].Y);
        var revealed = Exploration.Reveal(camp, Trip.Start(WorldMap.Places[WorldMap.Camp], near, DateTimeOffset.UnixEpoch), DateTimeOffset.UnixEpoch.AddHours(1));
        fog.Stop();

        var far = new TilePoint(WorldMap.Places[WorldMap.Camp].X + 2_000, WorldMap.Places[WorldMap.Camp].Y);
        var walk = Stopwatch.StartNew();
        var walked = Exploration.Reveal(camp, Trip.Start(WorldMap.Places[WorldMap.Camp], far, DateTimeOffset.UnixEpoch), DateTimeOffset.UnixEpoch.AddHours(1));
        walk.Stop();

        var ground = Stopwatch.StartNew();
        var wet = 0;
        for (var i = 0; i < 9 * 32 * 32; i++)
        {
            if (FarGround.Water(i % 32 - 100, i / 32, 7)) wet++;
        }
        ground.Stop();

        var line = $"join={join.Elapsed.TotalMilliseconds:0.0}ms wilds={wilds.Obstacles.Count} camp={camp.Length} fog={fog.Elapsed.TotalMilliseconds:0.0}ms fogBytes={revealed.Length} walk2000={walk.Elapsed.TotalMilliseconds:0.0}ms walkBytes={walked.Length} chunks={ground.Elapsed.TotalMilliseconds:0.0}ms wet={wet}";
        Assert.True(join.Elapsed.TotalMilliseconds < 100, line);
        Assert.True(fog.Elapsed.TotalMilliseconds < 50, line);
        Assert.True(walk.Elapsed.TotalMilliseconds < 200, line);
        Assert.True(walked.Length < 256 * 1024, line);
        Assert.True(ground.Elapsed.TotalMilliseconds < 50, line);
    }
}
