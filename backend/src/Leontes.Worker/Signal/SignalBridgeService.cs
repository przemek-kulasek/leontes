namespace Leontes.Worker.Signal;

public sealed class SignalBridgeService(ILogger<SignalBridgeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Signal bridge service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        logger.LogInformation("Signal bridge service stopping");
    }
}
