namespace Desertod.Api.Game;

/// <summary>
/// Lets beasts decide each night whether to attack, and fights the raids that struck even when nobody is looking.
/// </summary>
public sealed class RaidWorker(GameService game, TimeProvider time, ILogger<RaidWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, time);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var villageId in await game.FindRaidVillagesAsync(stoppingToken))
                {
                    try
                    {
                        await game.RaidVillageAsync(villageId, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogError(exception, "Raid failed in village {VillageId}.", villageId);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Raids failed.");
            }
        }
    }
}
