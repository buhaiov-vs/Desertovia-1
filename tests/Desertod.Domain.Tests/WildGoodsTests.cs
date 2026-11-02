using Desertod.Domain.Catalog;
using Desertod.Domain.Technologies;
using Desertod.Domain.World;

namespace Desertod.Domain.Tests;

public class WildGoodsTests
{
    [Fact]
    public void Patches_FillTheArcsBetweenDens_AndStayOffMountains()
    {
        var patches = WildGoods.Patches(1);
        Assert.Equal(160, patches.Count);
        var sample = string.Join("|", patches.OrderBy(patch => patch.Level).ThenBy(patch => patch.X).ThenBy(patch => patch.Y).Take(6)
            .Select(patch => $"{patch.X},{patch.Y},{patch.Kind},{patch.Level}"));
        Assert.Equal("-355,166,herbs,1|-341,-134,herbs,1|-137,381,herbs,1|-134,-347,herbs,1|167,-354,herbs,1|167,379,herbs,1", sample);
        Assert.Equal(patches.Select(patch => (patch.X, patch.Y)), WildGoods.Patches(1).Select(patch => (patch.X, patch.Y)));
        var camp = WorldMap.Places[WorldMap.Camp];
        var dens = SpawnRings.Points(1);
        var kinds = new HashSet<string>();
        foreach (var patch in patches)
        {
            kinds.Add(patch.Kind);
            Assert.Equal(WildGoods.KindFor(patch.Level), patch.Kind);
            var level = SpawnRings.Levels[patch.Level - 1];
            var distance = new TilePoint(patch.X + 0.5, patch.Y + 0.5).DistanceTo(camp);
            Assert.InRange(distance, level.Floor + 20, level.Floor + SpawnRings.Spread + 20);
            Assert.False(WorldMap.Vale.Contains(new TileArea(patch.X, patch.Y, 2, 2)));
            for (var dy = 0; dy < 2; dy++)
            {
                for (var dx = 0; dx < 2; dx++)
                {
                    Assert.False(FarWilds.Blocks(patch.X + dx, patch.Y + dy, 1));
                    Assert.Equal(patch.Kind, WildGoods.At(patch.X + dx, patch.Y + dy, 1));
                }
            }

            Assert.Null(WildGoods.At(patch.X + 2, patch.Y, 1));
            foreach (var den in dens.Where(den => den.Level == patch.Level))
            {
                var reach = den.Claim / 2 + WildGoods.ClearOfDen;
                var left = den.X - patch.X;
                var top = den.Y - patch.Y;
                Assert.True(left * left + top * top >= reach * reach);
            }
        }

        Assert.Contains(WildGoods.Herbs, kinds);
        Assert.Contains(WildGoods.Flint, kinds);
        Assert.Contains(WildGoods.Clay, kinds);
        Assert.Contains(WildGoods.Salt, kinds);
        Assert.Contains(WildGoods.Resin, kinds);
        Assert.Contains(WildGoods.Copper, kinds);
    }

    [Fact]
    public void Crafts_UseTheNewGoods()
    {
        var arrows = GameCatalog.FindTask("knapp-arrows")!;
        Assert.Contains(arrows.Inputs, input => input.Resource == ResourceKind.Flint);
        Assert.Contains(arrows.Outputs, output => output.Resource == ResourceKind.Arrows && output.Amount == 8);
        Assert.Equal(TechnologyCatalog.StoneTools, arrows.RequiredTechnology);

        var bricks = GameCatalog.FindTask("fire-bricks")!;
        Assert.Contains(bricks.Inputs, input => input.Resource == ResourceKind.Clay);
        Assert.Contains(bricks.Outputs, output => output.Resource == ResourceKind.Brick);

        var remedy = GameCatalog.FindTask("brew-herbs")!;
        Assert.Contains(remedy.Inputs, input => input.Resource == ResourceKind.Herbs);
        Assert.Equal(TechnologyCatalog.HerbLore, remedy.RequiredTechnology);

        var salted = GameCatalog.FindTask("salt-food")!;
        Assert.Contains(salted.Inputs, input => input.Resource == ResourceKind.Salt);
        Assert.Contains(salted.Outputs, output => output.Resource == ResourceKind.Food && output.Amount == 5);

        var bow = GameCatalog.FindTask("pitch-bow")!;
        Assert.Contains(bow.Inputs, input => input.Resource == ResourceKind.Resin);
        Assert.Equal(TechnologyCatalog.Archery, bow.RequiredTechnology);

        var copper = GameCatalog.FindTask("smelt-copper")!;
        Assert.Contains(copper.Inputs, input => input.Resource == ResourceKind.CopperOre);
        Assert.Contains(copper.Outputs, output => output.Resource == ResourceKind.Copper);

        var mechanism = GameCatalog.FindTask("wire-mechanism")!;
        Assert.Contains(mechanism.Inputs, input => input.Resource == ResourceKind.Copper);
        Assert.Equal(TechnologyCatalog.Mechanics, mechanism.RequiredTechnology);

        Assert.Equal(WildGoods.Herbs, GameCatalog.FindTask("gather-herbs")!.Workplace);
        Assert.Equal(2, GameCatalog.FindTask("gather-herbs")!.Outputs.Single().Amount);
    }
}
