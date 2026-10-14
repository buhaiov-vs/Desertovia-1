namespace Desertod.Api.Game;

/// <summary>Tells the viewers which citizens went away, so idle ones leave the map; see <see cref="CitizenPresence"/>.</summary>
public sealed class PresenceWorker(CitizenPresence presence, VillageNotifier notifier, TimeProvider time, ILogger<PresenceWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, time);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var (slug, citizenId) in presence.TakeDeparted())
                {
                    await notifier.CitizenAway(slug, citizenId);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Presence check failed.");
            }
        }
    }
}
