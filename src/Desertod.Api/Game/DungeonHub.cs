using Desertod.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Desertod.Api.Game;

/// <summary>
/// A dungeon run live: anyone signed in may watch; a hero's player sends where to walk, whom to strike and whom to heal.
/// The server decides everything else.
/// </summary>
[Authorize]
public sealed class DungeonHub(DungeonRuntime runtime, GameService game) : Hub
{
    public static string GroupName(Guid runId) => $"dungeon:{runId}";

    /// <summary>Starts sending snapshots of the run; returns its map and heroes, or null for an unknown run.</summary>
    public async Task<DungeonRunDto?> JoinRun(Guid runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(runId));
        var accountId = Context.User?.GetAccountId();
        var stored = await game.GetDungeonRunAsync(runId, accountId, Context.ConnectionAborted);
        if (stored is null || runtime.Find(runId) is not { } live)
        {
            return stored;
        }

        DungeonSnapshotDto? snapshot;
        lock (live.Gate)
        {
            snapshot = live.LastSnapshot ?? DungeonMapper.Snapshot(runId, live.Simulation, []);
        }

        return stored with { Heroes = live.Heroes, MyHeroId = accountId is { } id ? live.HeroOf(id) : null, Snapshot = snapshot };
    }

    public Task LeaveRun(Guid runId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(runId));

    public bool MoveTo(Guid runId, Guid heroId, double x, double y) =>
        Act(runId, heroId, live => live.Simulation.MoveTo(heroId, x, y));

    public bool Attack(Guid runId, Guid heroId, int foeId) =>
        Act(runId, heroId, live => live.Simulation.Attack(heroId, foeId));

    public bool Heal(Guid runId, Guid heroId, Guid allyId) =>
        Act(runId, heroId, live => live.Simulation.Heal(heroId, allyId));

    private bool Act(Guid runId, Guid heroId, Func<LiveDungeon, bool> intent)
    {
        if (Context.User?.GetAccountId() is not { } accountId || runtime.Find(runId) is not { } live || !live.Owns(heroId, accountId))
        {
            return false;
        }

        lock (live.Gate)
        {
            return intent(live);
        }
    }
}
