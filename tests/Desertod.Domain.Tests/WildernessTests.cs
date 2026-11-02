using Desertod.Domain.Buildings;
using Desertod.Domain.Catalog;
using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
using Desertod.Domain.Stats;
using Desertod.Domain.World;
using static Desertod.Domain.Tests.TestWorld;

namespace Desertod.Domain.Tests;

public class WildernessTests
{
    /// <summary>A small hand-made wild land: a level stretch of ravine, one thicket tile and the cave.</summary>
    private static Village Wild()
    {
        var village = NewVillage();
        village.Wilds = new Wilds
        {
            Ravine = Enumerable.Range(-12, 6).Select(x => new RavineSpan { X = x, Top = -20, Height = 2 }).ToList(),
            Obstacles =
            [
                new Obstacle { Kind = ObstacleKind.Thicket, X = -6, Y = 6 },
            ],
            CaveX = -20,
            CaveY = 0,
        };
        return village;
    }

    private static void See(Village village, double x, double y) => village.Explored = Exploration.Reveal(village.Explored, new TilePoint(x, y), 3);

    private static bool Seen(Village village, double x, double y) => Exploration.IsExplored(village.Explored, (int)Math.Floor(x), (int)Math.Floor(y));

    [Fact]
    public void NewVillage_GetsItsWildLandOnce_TheSameForTheSameVillage()
    {
        var village = Village.Create("w", "W", Guid.NewGuid(), Now);
        var wilds = village.Wilds!;

        Assert.False(village.EnsureCamp(Now));
        Assert.Same(wilds, village.Wilds);

        var again = Wilderness.Generate(village.Id);
        Assert.Equal(wilds.Ravine.Select(s => (s.X, s.Top)), again.Ravine.Select(s => (s.X, s.Top)));
        Assert.Equal(wilds.Obstacles.Select(o => (o.X, o.Y)), again.Obstacles.Select(o => (o.X, o.Y)));
        Assert.Equal((wilds.CaveX, wilds.CaveY), (again.CaveX, again.CaveY));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GeneratedRavine_RunsUnbrokenFromTheWestEdgeToTheRiver_AndLeavesTheCampAlone(int seed)
    {
        var wilds = Wilderness.Generate(new Guid(seed, 0, 0, new byte[8]));

        Assert.Equal(WorldMap.Vale.X, wilds.Ravine[0].X);
        for (var i = 1; i < wilds.Ravine.Count; i++)
        {
            Assert.Equal(wilds.Ravine[i - 1].X + 1, wilds.Ravine[i].X);
            Assert.InRange(Math.Abs(wilds.Ravine[i].Top - wilds.Ravine[i - 1].Top), 0, 1);
        }

        Assert.All(wilds.Ravine, span => Assert.True(span.Top + span.Height <= 0));
        Assert.Empty(wilds.Passes);
        Assert.DoesNotContain(wilds.Obstacles, o => o.Kind is ObstacleKind.Rockfall or ObstacleKind.Thicket);
        Assert.All(wilds.Obstacles, o =>
        {
            Assert.True(WorldMap.Land.Contains(o.Tile));
            Assert.False(WorldMap.BuildArea.Overlaps(o.Tile));
        });
        Assert.True(WorldMap.Land.Contains(new TileArea(wilds.CaveX, wilds.CaveY, 1, 1)));
        Assert.False(WorldMap.Vale.Contains(new TileArea(wilds.CaveX, wilds.CaveY, 1, 1)));
        var mouth = new TilePoint(wilds.CaveX + 0.5, wilds.CaveY + 0.5);
        Assert.InRange(mouth.DistanceTo(WorldMap.Places[WorldMap.Camp]), CaveSite.Near, CaveSite.Far);
        Assert.Equal((wilds.CaveX, wilds.CaveY), (CaveSite.At(Wilderness.Seed(new Guid(seed, 0, 0, new byte[8]).ToString())).X, CaveSite.At(Wilderness.Seed(new Guid(seed, 0, 0, new byte[8]).ToString())).Y));
        var mines = new[] { (wilds.CaveX, wilds.CaveY), (wilds.CoalX, wilds.CoalY), (wilds.IronX, wilds.IronY), (wilds.MoonX, wilds.MoonY) };
        Assert.Equal(4, mines.Distinct().Count());
        for (var i = 0; i < mines.Length; i++)
        {
            Assert.False(WorldMap.Vale.Contains(new TileArea(mines[i].Item1, mines[i].Item2, 1, 1)));
            for (var j = i + 1; j < mines.Length; j++)
            {
                var apart = new TilePoint(mines[i].Item1 + 0.5, mines[i].Item2 + 0.5)
                    .DistanceTo(new TilePoint(mines[j].Item1 + 0.5, mines[j].Item2 + 0.5));
                Assert.True(apart >= CaveSite.Apart);
            }
        }
        var gold = wilds.Gold.Select(vein => vein.Level).OrderBy(level => level).ToArray();
        Assert.Equal(GoldVein.Levels, gold);
        Assert.All(wilds.Gold, vein =>
        {
            Assert.False(WorldMap.Vale.Contains(vein.Center.ToTile()));
            Assert.True(vein.Center.DistanceTo(WorldMap.Places[WorldMap.Camp]) > 1_000);
            var den = SpawnRings.Points(Wilderness.Seed(new Guid(seed, 0, 0, new byte[8]).ToString())).First(point => point.Level == vein.Level);
            Assert.True(vein.Center.DistanceTo(den.Center) > den.Claim / 2.0);
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GeneratedThickets_StayOffTheCamp(int seed)
    {
        var wilds = Wilderness.Generate(new Guid(seed, 0, 0, new byte[8]));

        Assert.DoesNotContain(wilds.Obstacles, o => o.Kind is ObstacleKind.Thicket or ObstacleKind.Rockfall);
        Assert.Empty(wilds.Passes);
    }

    [Fact]
    public void OldThicketBlob_GrowsIntoAHedge_Once()
    {
        var village = NewVillage();
        village.Wilds = new Wilds
        {
            Ravine = [],
            Obstacles =
            [
                new Obstacle { Kind = ObstacleKind.Thicket, X = -30, Y = 40 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -31, Y = 40 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -29, Y = 40 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -30, Y = 39 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -30, Y = 41 },
            ],
            CaveX = -20,
            CaveY = 0,
        };

        Assert.True(Wilderness.ClearAroundVillage(village));
        Assert.Empty(village.Wilds.Obstacles);
        Assert.False(Wilderness.ClearAroundVillage(village));
    }

    [Fact]
    public void StraightThicket_IsLeftAlone()
    {
        var village = NewVillage();
        village.Wilds = new Wilds
        {
            Obstacles = Enumerable.Range(0, 8).SelectMany(step => new[]
            {
                new Obstacle { Kind = ObstacleKind.Thicket, X = -30 + step, Y = 40 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -30 + step, Y = 41 },
            }).ToList(),
        };

        Assert.True(Wilderness.ClearAroundVillage(village));
        Assert.Empty(village.Wilds.Obstacles);
        Assert.False(Wilderness.ClearAroundVillage(village));
    }

    [Fact]
    public void ClearedThicket_IsNotFilledIn()
    {
        var village = NewVillage();
        village.Wilds = new Wilds
        {
            Obstacles =
            [
                new Obstacle { Kind = ObstacleKind.Thicket, X = -30, Y = 40 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -31, Y = 40 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -29, Y = 40 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -30, Y = 39 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -30, Y = 41 },
                new Obstacle { Kind = ObstacleKind.Thicket, X = -31, Y = 39, ClearedAt = Now },
            ],
        };

        Assert.True(Wilderness.ClearAroundVillage(village));
        Assert.Empty(village.Wilds.Obstacles);
    }

    [Fact]
    public void GeneratedObstacles_KeepOffTheCampLand_AndTheRoadEast()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var wilds = Wilderness.Generate(new Guid(seed, 7, 11, new byte[8]));

            Assert.All(wilds.Obstacles, o =>
            {
                Assert.False(o.X is >= 0 and < 44 && o.Y is >= 0 and < 26);
                Assert.False(o.X > 33 && o.Y is 12 or 13);
            });
        }
    }

    [Fact]
    public void UnseenThicket_CannotBeCleared()
    {
        var village = Wild();
        var citizen = NewCitizen(village);
        var context = new WorkContext { Village = village };

        Assert.Equal("wilds.noThicket", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("clear-thicket"), context, Now)).Code);
    }

    [Fact]
    public void ClearingTheThicket_GivesWood_AndOpensTheWay()
    {
        var village = Wild();

        // A fixed camp: about one random camp in fifty has no way from the campfire to this thicket.
        village.Id = new Guid("00000000-0000-7000-8000-000000000001");
        See(village, -5.5, 6.5);
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village };

        CycleEngine.Start(citizen, Task("clear-thicket"), context, Now);
        var report = Finish(citizen, context);

        Assert.Equal(new TilePoint(-5.5, 6.5), citizen.Spot);
        Assert.Contains(report.Produced, p => p.Resource == ResourceKind.Wood && p.Amount > 0);
        Assert.NotNull(village.Wilds!.Obstacles[0].ClearedAt);
        Assert.Equal("wilds.noThicket", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("clear-thicket"), context, citizen.CycleEndsAt!.Value)).Code);
    }

    [Fact]
    public void Tunnel_IsDugOnlyAtAPickedMountainFace()
    {
        var village = TunnelVillage(out var face, out var inner);
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var open = new TilePoint(face.X - 0.5, face.Y + 0.5);
        citizen.IdleSpot = open;
        var context = new WorkContext { Village = village };

        Assert.Equal("tunnel.pickTile", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("dig-tunnel"), context, Now)).Code);
        Assert.Equal("tunnel.notMountain", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("dig-tunnel"), context, Now, open)).Code);
        Assert.Equal("tunnel.notFace", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("dig-tunnel"), context, Now, Centre(inner))).Code);
    }

    [Fact]
    public void Tunnel_OpensOneTileACycle_GivesStone_AndLetsTheNextTileBeDug()
    {
        var village = TunnelVillage(out var face, out var inner);
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        citizen.IdleSpot = new TilePoint(face.X - 0.5, face.Y + 0.5);
        var context = new WorkContext { Village = village };

        CycleEngine.Start(citizen, Task("dig-tunnel"), context, Now, Centre(face));
        var report = Finish(citizen, context);

        var dug = Assert.Single(village.Wilds!.Obstacles, o => o.Kind == ObstacleKind.Tunnel);
        Assert.Equal((face.X, face.Y), (dug.X, dug.Y));
        Assert.NotNull(dug.ClearedAt);
        Assert.Contains(report.Produced, p => p.Resource == ResourceKind.Stone && p.Amount > 0);
        Assert.Equal(480, Task("dig-tunnel").WorkSeconds);

        citizen.StoredEnergy = citizen.MaximumEnergy;
        citizen.IdleSpot = Centre(face);
        CycleEngine.Start(citizen, Task("dig-tunnel"), context, citizen.CycleEndsAt!.Value, Centre(inner));
        Finish(citizen, context);

        Assert.Equal(2, village.Wilds.Obstacles.Count(o => o.Kind == ObstacleKind.Tunnel && o.ClearedAt is not null));
    }

    [Fact]
    public void ClearAround_KeepsFarThicketsAndTunnels_AndDropsEveryRockfall()
    {
        var village = NewVillage();
        village.Wilds = new Wilds
        {
            Obstacles =
            [
                new Obstacle { Kind = ObstacleKind.Thicket, X = 400, Y = 10, ClearedAt = Now },
                new Obstacle { Kind = ObstacleKind.Tunnel, X = 401, Y = 10, ClearedAt = Now },
                new Obstacle { Kind = ObstacleKind.Rockfall, X = 402, Y = 10, ClearedAt = Now },
                new Obstacle { Kind = ObstacleKind.Rockfall, X = 403, Y = 10 },
            ],
        };

        Assert.True(Wilderness.ClearAroundVillage(village));
        Assert.Equal([ObstacleKind.Thicket, ObstacleKind.Tunnel], village.Wilds.Obstacles.Select(o => o.Kind));
        Assert.False(Wilderness.ClearAroundVillage(village));
    }

    private static TilePoint Centre((int X, int Y) tile) => new(tile.X + 0.5, tile.Y + 0.5);

    /// <summary>
    /// A fixed camp with a mountain face the campfire can walk to: open ground west of the face, mountain under it and
    /// one more mountain tile east of it. Both tiles are already seen.
    /// </summary>
    private static Village TunnelVillage(out (int X, int Y) face, out (int X, int Y) inner)
    {
        var village = Wild();
        village.Id = new Guid("00000000-0000-7000-8000-000000000001");
        var seed = Wilderness.Seed(village.Id.ToString());
        var camp = WorldMap.Places[WorldMap.Camp];
        for (var radius = 130; radius < 2_000; radius += 7)
        {
            for (var step = 0; step < 64; step++)
            {
                var x = (int)(camp.X + radius * Math.Cos(step * Math.PI / 32));
                var y = (int)(camp.Y + radius * Math.Sin(step * Math.PI / 32));
                if (FarWilds.At(x - 1, y, seed) != FarWilds.Kind.Open
                    || FarWilds.At(x, y, seed) != FarWilds.Kind.Mountain
                    || FarWilds.At(x + 1, y, seed) != FarWilds.Kind.Mountain
                    || FarWilds.At(x + 1, y - 1, seed) == FarWilds.Kind.Open
                    || FarWilds.At(x + 1, y + 1, seed) == FarWilds.Kind.Open
                    || FarWilds.At(x + 2, y, seed) == FarWilds.Kind.Open)
                {
                    continue;
                }

                if (OverlandPath.Find(village, camp, new TilePoint(x + 0.5, y + 0.5)) is null)
                {
                    continue;
                }

                face = (x, y);
                inner = (x + 1, y);
                See(village, x + 0.5, y + 0.5);
                return village;
            }
        }

        throw new InvalidOperationException("No reachable mountain face near this camp.");
    }

    [Fact]
    public void Cave_IsDugOnceFound_MostlyForStone()
    {
        var village = Wild();
        var citizen = NewCitizen(village, (Stat.Strength, 3), (Stat.Endurance, 3));
        var context = new WorkContext { Village = village };

        Assert.Equal("wilds.caveUnknown", Assert.Throws<GameRuleException>(() =>
            CycleEngine.Start(citizen, Task("dig-cave"), context, Now)).Code);

        See(village, -19.5, 0.5);
        CycleEngine.Start(citizen, Task("dig-cave"), context, Now);
        var report = Finish(citizen, context);

        Assert.Equal(village.Wilds!.CaveMouth, citizen.Spot);
        Assert.Contains(report.Produced, p => p.Resource == ResourceKind.Stone && p.Amount > 0);
        Assert.All(report.Produced, p => Assert.Equal(ResourceKind.Stone, p.Resource));
    }

    [Fact]
    public void RopeBridge_MustSpanTheRavine()
    {
        var village = Wild();
        village.Explored = Exploration.Reveal(village.Explored, new TilePoint(-9, -19), 5);

        Assert.Equal("building.bridgeNeedsRavine", Assert.Throws<GameRuleException>(() =>
            ConstructionRules.Place(village, BuildingKind.RopeBridge, 2, 2, Now)).Code);
        Assert.Equal("building.bridgeNeedsRavine", Assert.Throws<GameRuleException>(() =>
            ConstructionRules.Place(village, BuildingKind.RopeBridge, -10, -20, Now)).Code);

        var site = ConstructionRules.Place(village, BuildingKind.RopeBridge, -10, -21, Now);

        Assert.Equal(BuildingKind.RopeBridge, site.Kind);
        Assert.Equal(["posts", "ropes", "deck"], site.Definition.Stages.Select(s => s.Id));
    }

    [Fact]
    public void FarThicket_IsRecordedWhenCleared_AndNotFilledBack()
    {
        var village = Wild();
        var seed = Wilderness.Seed(village.Id.ToString());
        int? foundX = null;
        int? foundY = null;
        var land = WorldMap.Land;
        for (var y = land.Y; foundX is null && y < land.Y + land.Height; y += 16)
        {
            for (var x = land.X; x < land.X + land.Width; x += 16)
            {
                if (FarWilds.At(x, y, seed) != FarWilds.Kind.Thicket) continue;
                foundX = x;
                foundY = y;
                break;
            }
        }

        Assert.NotNull(foundX);
        var spot = new TilePoint(foundX!.Value + 0.5, foundY!.Value + 0.5);
        Assert.True(Wilderness.Clear(village, ObstacleKind.Thicket, 3, spot, Now) > 0);
        Assert.Contains(village.Wilds!.Obstacles, obstacle => obstacle.X == foundX && obstacle.Y == foundY && obstacle.ClearedAt is not null);
        var count = village.Wilds.Obstacles.Count;
        Wilderness.Clear(village, ObstacleKind.Thicket, 3, spot, Now);
        Assert.Equal(count, village.Wilds.Obstacles.Count);
        Assert.Contains(village.Wilds.Obstacles, obstacle => obstacle.X == foundX && obstacle.Y == foundY && obstacle.ClearedAt is not null);
    }
}
