using Desertod.Domain.Entities;
using Desertod.Domain.Rules;
namespace Desertod.Domain.World;

/// <summary>One server rule for every destination; the river continues beyond the initial viewport.</summary>
public static class BankAccess
{
    public static bool CanReach(Village village, TilePoint point) => village.BridgeOpened && village.BridgeRuinedAt is null ||
        point.X <= Wilderness.RiverCenter((int)Math.Floor(point.Y), Wilderness.Seed(village.Id.ToString()));
    public static void Require(Village village, TilePoint point)
    {
        if (!WorldMap.Land.Contains(point.ToTile())) throw new GameRuleException("spot.outsideLand", "This spot is beyond the land.");
        if (!CanReach(village, point)) throw new GameRuleException("bridge.closed", "Build the Tsar Bridge before crossing the river.");
    }
}
