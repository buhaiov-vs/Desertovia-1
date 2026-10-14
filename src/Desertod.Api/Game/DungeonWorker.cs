using Desertod.Domain.Dungeons;
using Microsoft.AspNetCore.SignalR;

namespace Desertod.Api.Game;

/// <summary>
/// Runs the dungeons: closes gatherings and marches once a second, and ticks the fights ten times a second, sending
/// each run's snapshot to its viewers. Heroes who are out, and runs that are over, are settled in the database.
/// </summary>
public sealed class DungeonWorker(
    GameService game,
    DungeonRuntime runtime,
    IHubContext<DungeonHub> hub,
    TimeProvider time,
    ILogger<DungeonWorker> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PhaseInterval = TimeSpan.FromSeconds(1);

    /// <summary>A late tick moves the fight by this much at most, so a stall does not teleport anyone.</summary>
    private const double LongestTickSeconds = 0.5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await game.AbandonDungeonsAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Abandoning unfinished dungeon runs failed.");
        }

        using var timer = new PeriodicTimer(TickInterval);
        var lastTick = time.GetTimestamp();
        long? lastPhases = null;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (lastPhases is not { } phases || time.GetElapsedTime(phases) >= PhaseInterval)
            {
                lastPhases = time.GetTimestamp();
                await AdvancePhasesAsync(stoppingToken);
            }

            var seconds = Math.Min(LongestTickSeconds, time.GetElapsedTime(lastTick).TotalSeconds);
            lastTick = time.GetTimestamp();
            foreach (var live in runtime.Runs.ToList())
            {
                try
                {
                    await TickAsync(live, seconds, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Dungeon run {RunId} failed to tick.", live.RunId);
                }
            }
        }
    }

    private async Task AdvancePhasesAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var start in await game.AdvanceDungeonsAsync(cancellationToken))
            {
                runtime.Start(start);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Advancing dungeon runs failed.");
        }
    }

    private async Task TickAsync(LiveDungeon live, double seconds, CancellationToken cancellationToken)
    {
        var exits = new List<DungeonExit>();
        DungeonOutcome? outcome = null;
        int slain;
        DungeonSnapshotDto snapshot;
        lock (live.Gate)
        {
            var simulation = live.Simulation;
            var events = simulation.Tick(seconds, live.Random);
            foreach (var hero in simulation.Heroes)
            {
                if ((hero.State == HeroState.Left || simulation.IsOver) && live.Settled.Add(hero.Id))
                {
                    exits.Add(new DungeonExit(hero.Id, simulation.OutcomeOf(hero)));
                }
            }

            if (simulation.IsOver)
            {
                outcome = simulation.Outcome;
            }

            slain = simulation.Slain;
            snapshot = DungeonMapper.Snapshot(live.RunId, simulation, events);
            live.LastSnapshot = snapshot;
        }

        await hub.Clients.Group(DungeonHub.GroupName(live.RunId)).SendAsync("dungeonSnapshot", snapshot, cancellationToken);
        if (exits.Count == 0 && outcome is null)
        {
            return;
        }

        try
        {
            await game.SettleDungeonAsync(live.RunId, live.VillageId, exits, outcome, slain, cancellationToken);
        }
        finally
        {
            if (outcome is not null)
            {
                runtime.Remove(live.RunId);
                await hub.Clients.Group(DungeonHub.GroupName(live.RunId)).SendAsync("dungeonEnded", live.RunId, cancellationToken);
            }
        }
    }
}
