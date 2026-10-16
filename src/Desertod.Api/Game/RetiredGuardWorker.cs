namespace Desertod.Api.Game;

/// <summary>Finishes only already-paid retired duties. Never starts a cycle or touches medical restrictions.</summary>
public sealed class RetiredGuardWorker(GameService game,TimeProvider time,ILogger<RetiredGuardWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try{if(!await game.RetireGuardDutiesAsync(stoppingToken))return;}
            catch(Exception e) when(e is not OperationCanceledException){logger.LogWarning("Retired duty settlement will retry ({Type}).",e.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(15),time,stoppingToken);
        }
    }
}
