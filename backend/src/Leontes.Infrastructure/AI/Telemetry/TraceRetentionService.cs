using Leontes.Application.Configuration;
using Leontes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Deletes raw pipeline traces older than <see cref="TelemetryOptions.TraceRetentionDays"/>.
/// Runs daily. Aggregated <c>MetricsSummaries</c> are preserved indefinitely.
/// </summary>
internal sealed class TraceRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<TelemetryOptions> options,
    ILogger<TraceRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        try
        {
            do
            {
                try
                {
                    await PruneAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Trace retention prune failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PruneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, options.Value.TraceRetentionDays));

        var deleted = await db.PipelineTraceSet
            .Where(t => t.StartedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
            logger.LogInformation("Pruned {Count} pipeline traces older than {Cutoff:u}", deleted, cutoff);
    }
}
