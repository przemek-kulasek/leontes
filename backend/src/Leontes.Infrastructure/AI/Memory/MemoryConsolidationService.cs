using Leontes.Application;
using Leontes.Application.Configuration;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.Memory;

/// <summary>
/// Periodically distills raw observations into higher-level insights.
/// Runs in the background and never blocks the request pipeline.
/// </summary>
public sealed class MemoryConsolidationService(
    IServiceScopeFactory scopeFactory,
    IOptions<MemoryOptions> options,
    ILogger<MemoryConsolidationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.ConsolidationIntervalHours));
        logger.LogInformation("Memory consolidation scheduled every {Interval}", interval);

        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ConsolidateAsync(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Memory consolidation run failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }
    }

    internal async Task ConsolidateAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var memoryStore = scope.ServiceProvider.GetRequiredService<IMemoryStore>();
        var chatClient = scope.ServiceProvider.GetRequiredKeyedService<IChatClient>("Small");

        var since = DateTime.UtcNow - window;

        var observations = await db.MemoryEntries
            .AsNoTracking()
            .Where(m => m.Type == MemoryType.Observation && m.Created >= since)
            .OrderBy(m => m.Created)
            .ToListAsync(cancellationToken);

        if (observations.Count < 2)
        {
            logger.LogDebug(
                "Skipping consolidation: {Count} observations since {Since}",
                observations.Count, since);
            return;
        }

        var grouped = observations
            .GroupBy(o => o.SourceConversationId ?? Guid.Empty)
            .Where(g => g.Count() >= 2);

        foreach (var group in grouped)
        {
            var insights = await DistillAsync(group.ToList(), chatClient, cancellationToken);
            foreach (var insight in insights)
            {
                await memoryStore.StoreAsync(
                    insight,
                    MemoryType.Insight,
                    sourceMessageId: null,
                    sourceConversationId: group.Key == Guid.Empty ? null : group.Key,
                    importance: 0.7f,
                    cancellationToken);
            }

            logger.LogInformation(
                "Consolidated {ObservationCount} observations into {InsightCount} insights for conversation {ConversationId}",
                group.Count(), insights.Count, group.Key);
        }
    }

    private static async Task<IReadOnlyList<string>> DistillAsync(
        IReadOnlyList<MemoryEntry> observations,
        IChatClient chatClient,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(observations);
        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            cancellationToken: cancellationToken);

        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TrimBullet)
            .Where(static line => line.Length > 0)
            .Take(3)];
    }

    private static string TrimBullet(string line)
    {
        var span = line.AsSpan();
        while (span.Length > 0 && (span[0] == '-' || span[0] == '*' || char.IsDigit(span[0]) || span[0] == '.' || span[0] == ' '))
            span = span[1..];
        return span.ToString().Trim();
    }

    private static string BuildPrompt(IReadOnlyList<MemoryEntry> observations)
    {
        var joined = string.Join("\n", observations.Select(o => $"- {o.Content}"));
        return $"""
            Distill the following observations into 1-3 concise insights about the user's goals, preferences, or work context.
            Return one insight per line. No commentary, no numbering, no prefixes.

            Observations:
            {joined}
            """;
    }
}
