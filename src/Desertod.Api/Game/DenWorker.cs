using Desertod.Domain.Combat;

namespace Desertod.Api.Game;

/// <summary>
/// Runs the den fights, and the fights with the roaming foes of <see cref="Domain.World.Haunts"/>, which are dens of
/// one protector. Twice a second it looks for present citizens walking into a den's reach and stops them for
/// the fight; ten times a second it ticks the dens that are awake, sends their state to the village, and writes the
/// citizens who came out of a fight to the database.
/// </summary>
public sealed class DenWorker(
    GameService game,
    DenRuntime runtime,
    CitizenPresence presence,
    VillageNotifier notifier,
    TimeProvider time,
    ILogger<DenWorker> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>A late tick moves the fight by this much at most, so a stall does not teleport anyone.</summary>
    private const double LongestTickSeconds = 0.5;

    private readonly Dictionary<LiveDen, Task> settlements = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var loops = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var scans = ScanLoopAsync(loops.Token);
        var fights = FightLoopAsync(loops.Token);
        await Task.WhenAny(scans, fights);
        await loops.CancelAsync();
        try
        {
            await Task.WhenAll(scans, fights);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ScanLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DropOutAsync(stoppingToken);
                await WakeAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Looking for den fights failed.");
            }
        }
    }

    private async Task DropOutAsync(CancellationToken cancellationToken)
    {
        var fighting = new Dictionary<Guid, LiveDen>();
        foreach (var live in runtime.Dens)
        {
            lock (live.Gate)
            {
                if (live.Fight.Hero is { } hero)
                {
                    fighting[hero.CitizenId] = live;
                }
            }
        }

        foreach (var citizenId in await game.FindDenDropoutsAsync(fighting.Keys, cancellationToken))
        {
            var live = fighting[citizenId];
            lock (live.Gate)
            {
                live.Fight.Withdraw(citizenId);
            }
        }
    }

    private async Task WakeAsync(CancellationToken cancellationToken)
    {
        foreach (var waker in await game.FindDenWakersAsync(presence.PresentCitizens(), cancellationToken))
        {
            if (runtime.Fighting(waker.CitizenId) is not null)
            {
                continue;
            }

            var now = time.GetUtcNow();
            var (point, _) = runtime.Near(waker.VillageId, waker.Position, DenGuards.AggroRange, now)
                .MinBy(near => near.Where.DistanceTo(waker.Position));
            if (point is null)
            {
                continue;
            }

            var live = runtime.Open(waker.VillageId, waker.Slug, point, now);
            lock (live.Gate)
            {
                if (live.Opening || !live.Fight.Wakes(waker.Position))
                {
                    continue;
                }

                live.Opening = true;
            }

            try
            {
                if (await game.EngageDenAsync(waker, point.Id, cancellationToken) is { } engagement)
                {
                    lock (live.Gate)
                    {
                        if (live.Fight.Engage(engagement.Fighter, engagement.Position, engagement.Destination))
                        {
                            live.HeroAccountId = waker.AccountId;
                        }
                    }
                }
            }
            finally
            {
                lock (live.Gate)
                {
                    live.Opening = false;
                }
            }
        }
    }

    private async Task FightLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        var lastTick = time.GetTimestamp();
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var seconds = Math.Min(LongestTickSeconds, time.GetElapsedTime(lastTick).TotalSeconds);
                lastTick = time.GetTimestamp();
                foreach (var done in settlements.Where(pair => pair.Value.IsCompleted).Select(pair => pair.Key).ToList())
                {
                    settlements.Remove(done);
                }

                foreach (var live in runtime.Dens.ToList())
                {
                    try
                    {
                        await TickAsync(live, seconds, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogError(exception, "Den {DenId} of village {Slug} failed to tick.", live.Id, live.Slug);
                    }
                }
            }
        }
        finally
        {
            await Task.WhenAll(settlements.Values);
        }
    }

    private async Task TickAsync(LiveDen live, double seconds, CancellationToken cancellationToken)
    {
        DenSnapshotDto? snapshot = null;
        var remove = false;
        List<DenOutcome> unsettled;
        lock (live.Gate)
        {
            var fight = live.Fight;
            if (live.HeroAccountId is { } accountId && fight.Hero is { } fighter && game.HasHospitalRestriction(accountId))
            {
                fight.Withdraw(fighter.CitizenId);
            }

            var wasResting = fight.IsResting;
            var events = fight.Tick(seconds, time.GetUtcNow(), live.Random);
            if (fight.TakeReleased() is { } outcome)
            {
                live.Unsettled.Add(outcome);
                live.HeroAccountId = null;
            }

            var moving = fight.Hero is not null || fight.Protectors.Any(p => p.State is DenGuardState.Chasing or DenGuardState.Returning);
            if (events.Count > 0 || moving || (!wasResting && fight.IsResting))
            {
                snapshot = live.Snapshot(events);
            }

            remove = fight.IsResting && !live.Opening && live.Unsettled.Count == 0;
            unsettled = [.. live.Unsettled];
        }

        if (snapshot is not null)
        {
            await notifier.DenUpdated(live.Slug, snapshot);
        }

        if (unsettled.Count > 0 && !settlements.ContainsKey(live))
        {
            settlements[live] = SettleAsync(live, unsettled, cancellationToken);
        }

        if (remove)
        {
            runtime.Remove(live);
        }
    }

    private async Task SettleAsync(LiveDen live, IReadOnlyList<DenOutcome> outcomes, CancellationToken cancellationToken)
    {
        foreach (var outcome in outcomes)
        {
            try
            {
                await game.SettleDenAsync(live.VillageId, live.Id, outcome, cancellationToken);
            }
            catch (Exception exception)
            {
                // Kept in Unsettled and retried on a later tick, so no reward or wound is lost.
                if (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Settling den {DenId} of village {Slug} failed; retained for retry.", live.Id, live.Slug);
                }

                return;
            }

            lock (live.Gate)
            {
                live.Unsettled.Remove(outcome);
            }
        }
    }
}
