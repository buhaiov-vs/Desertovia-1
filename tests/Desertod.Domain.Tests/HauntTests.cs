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

    [Fact]
    public void NineInTenFoes_AreOfTheirBand_AndNoneStandsNearTheCampOrADen()
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
                var band = SpawnRings.Levels.Count(level => level.Floor <= distance);
                Assert.InRange(foe.Level, band - 1, band + 1);
                Assert.True(distance >= SpawnRings.Levels[0].Floor);
                Assert.All(dens, den => Assert.True(den.Center.DistanceTo(foe.Center) >= Haunts.DenClearance));
                Assert.True(foe.Roams);
                if (foe.Level == band) own++;
                else off++;
            }
        }

        Assert.InRange((double)off / (own + off), 0.07, 0.13);
    }

    [Fact]
    public void AFoe_FightsAsOneProtectorOfItsLevel_OnItsOwnTile()
    {
        var dens = SpawnRings.Points(1);
        var foe = Enumerable.Range(10, 40).Select(cell => Haunts.At(1, cell, 0, dens)).First(foe => foe is not null)!;
        var fight = new DenFight(foe, WorldMap.Places[WorldMap.Camp], (_, _) => true);

        var protector = Assert.Single(fight.Protectors);
        Assert.Equal(foe.Center, protector.Home);
        Assert.Equal(DenGuards.For(SpawnRings.Levels[foe.Level - 1]).Health, protector.Health);
        Assert.Equal(Haunts.Claim / 2.0, fight.Leash);
        Assert.True(fight.Wakes(new TilePoint(foe.Center.X + DenGuards.AggroRange - 0.5, foe.Center.Y)));
        Assert.False(fight.Wakes(new TilePoint(foe.Center.X + DenGuards.AggroRange + 0.5, foe.Center.Y)));
        Assert.Contains(Haunts.Near(1, foe.Center, DenGuards.AggroRange, dens), near => near.Id == foe.Id);
    }
}
