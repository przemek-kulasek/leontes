using Leontes.Application.Configuration;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Periodically rolls up the raw <see cref="PipelineTrace"/> stream into
/// hourly <see cref="MetricsSummary"/> rows. Each period is aggregated once —
/// the unique (PeriodStart, PeriodEnd) index on the summary table prevents duplicates.
/// </summary>
internal sealed class MetricsAggregationService(
    IServiceScopeFactory scopeFactory,
    IOptions<TelemetryOptions> options,
    ILogger<MetricsAggregationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
            return;

        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.MetricsAggregationIntervalMinutes));
        logger.LogInformation("Metrics aggregation scheduled every {Interval}", interval);

        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await AggregateAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Metrics aggregation cycle failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task AggregateAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var periodEnd = FloorToHour(DateTime.UtcNow);
        var periodStart = periodEnd.AddHours(-1);

        var exists = await db.MetricsSummarySet.AnyAsync(
            m => m.PeriodStart == periodStart && m.PeriodEnd == periodEnd,
            cancellationToken);
        if (exists)
            return;

        var traces = await db.PipelineTraceSet
            .AsNoTracking()
            .Where(t => t.StartedAt >= periodStart && t.StartedAt < periodEnd)
            .ToListAsync(cancellationToken);

        if (traces.Count == 0)
            return;

        var latencies = traces
            .Where(t => t.CompletedAt is not null)
            .Select(t => (t.CompletedAt!.Value - t.StartedAt).TotalMilliseconds)
            .OrderBy(ms => ms)
            .ToArray();

        var summary = new MetricsSummary
        {
            Id = Guid.NewGuid(),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TotalRequests = traces.Count,
            SuccessfulRequests = traces.Count(t => t.Outcome == PipelineOutcome.Success),
            DegradedRequests = traces.Count(t =>
                t.Outcome == PipelineOutcome.DegradedSuccess || t.Outcome == PipelineOutcome.PartialSuccess),
            FailedRequests = traces.Count(t => t.Outcome == PipelineOutcome.Failed),
            MedianLatencyMs = Percentile(latencies, 0.50),
            P95LatencyMs = Percentile(latencies, 0.95),
            TotalInputTokens = traces.Sum(t => t.TotalInputTokens),
            TotalOutputTokens = traces.Sum(t => t.TotalOutputTokens),
            MemoryHitRate = 0,
            ToolSuccessRate = 0,
            SentinelEventsProcessed = 0,
            SentinelEventsDropped = 0
        };

        db.MetricsSummarySet.Add(summary);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Aggregated {Count} traces into summary for {PeriodStart:u}",
            traces.Count, periodStart);
    }

    private static DateTime FloorToHour(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);

    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0)
            return 0;

        var rank = percentile * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
            return sorted[lower];

        var weight = rank - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}
