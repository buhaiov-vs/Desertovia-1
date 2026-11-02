using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public class FarWalkTests
{
    public sealed record FarWalkCase(uint Seed, string From, string To, string[]? Way);

    public static IEnumerable<object[]> FarWalkCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "far-walks.json");
        return JsonSerializer.Deserialize<FarWalkCase[]>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Select(sample => new object[] { sample });
    }

    [Theory]
    [MemberData(nameof(FarWalkCases))]
    public void Turns_MatchTheOnesTheBrowserTakes(FarWalkCase sample)
    {
        var terrain = new byte[AuthoredTerrain.Width * AuthoredTerrain.Height];
        var way = OverlandPath.Find(terrain, Point(sample.From), Point(sample.To), true, [], sample.Seed);

        Assert.Equal(sample.Way, way?.Select(point => Text(point)).ToArray());
    }

    [Fact]
    public void AWalkAcrossTheWholeLand_IsAnsweredQuickly()
    {
        const uint seed = 3;
        var terrain = new byte[AuthoredTerrain.Width * AuthoredTerrain.Height];
        var from = new TilePoint(WorldMap.Land.X + 20.5, 13.5);
        var to = new TilePoint(WorldMap.Land.X + WorldMap.Land.Width - 20.5, 13.5);
        FarWilds.At(1_000, 1_000, seed);

        var watch = Stopwatch.StartNew();
        OverlandPath.Find(terrain, from, to, true, [], seed);
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 1_500, $"a walk across the land took {watch.ElapsedMilliseconds} ms");
    }

    private static TilePoint Point(string text)
    {
        var parts = text.Split(',');

        return new TilePoint(double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    private static string Text(TilePoint point) =>
        $"{point.X.ToString(CultureInfo.InvariantCulture)},{point.Y.ToString(CultureInfo.InvariantCulture)}";
}
