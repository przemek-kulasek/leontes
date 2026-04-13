namespace Leontes.Worker.Signal;

public sealed class SignalBridgeService(
    ILogger<SignalBridgeService> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var apiKey = configuration["Authentication:ApiKey"];

        if (string.IsNullOrEmpty(apiKey))
            logger.LogWarning("Authentication:ApiKey is not configured — Signal bridge will not be able to authenticate with the API");

        logger.LogInformation("Signal bridge service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        logger.LogInformation("Signal bridge service stopping");
    }
}
