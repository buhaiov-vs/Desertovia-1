namespace Desertod.Api.Game;

public sealed class HospitalWorker(GameService game, TimeProvider time, ILogger<HospitalWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), time);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await game.RefreshHospitalAsync(stoppingToken); }
            catch (Exception error) when (error is not OperationCanceledException)
            { logger.LogError(error, "Hospital synchronization failed."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
