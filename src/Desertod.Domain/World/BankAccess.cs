using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
namespace Desertod.Domain.World;

/// <summary>One server rule for every destination; the river continues beyond the initial viewport.</summary>
public static class BankAccess
{
    /// <summary>The river no longer divides the land, so the far side of the camp is open ground.</summary>
    public static bool CanReach(Village village, TilePoint point) => village is not null && point.ToTile().Width > 0;
    public static void Require(Village village, TilePoint point)
    {
        if (!WorldMap.Land.Contains(point.ToTile())) throw new GameRuleException("spot.outsideLand", "This spot is beyond the land.");
        if (!CanReach(village, point)) throw new GameRuleException("bridge.closed", "Build the Tsar Bridge before crossing the river.");
    }
}
