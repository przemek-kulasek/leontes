using Leontes.Application;
using Leontes.Application.Configuration;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

/// <summary>
/// Trims enrichment/history on the supplied <see cref="ThinkingContext"/> until
/// the assembled prompt fits the model's context window. Mutates the passed
/// instance in place (matching the Perceive/Enrich/Plan/Execute mutation
/// pattern) and returns it. Strategy: drop low-relevance memories → summarize
/// older history → truncate. Summary messages are persisted with
/// Role=Summary for history/observability.
/// </summary>
public sealed class ContextWindowManager(
    [FromKeyedServices("Small")] IChatClient summarizer,
    IServiceScopeFactory scopeFactory,
    IOptions<ResilienceOptions> options,
    ILogger<ContextWindowManager> logger) : IContextWindowManager
{
    private readonly ContextWindowOptions _options = options.Value.ContextWindow;

    public async Task<ThinkingContext> FitAsync(
        ThinkingContext context,
        int modelTokenLimit,
        CancellationToken cancellationToken)
    {
        var budget = Math.Max(256, modelTokenLimit - (modelTokenLimit * Math.Max(0, _options.BufferPercentage) / 100));
        var charsPerToken = Math.Max(1, _options.AverageCharsPerToken);

        var baseChars = (context.UserContent?.Length ?? 0) + (context.Plan?.Length ?? 0);
        var memoryChars = context.RelevantMemories.Sum(m => m.Content.Length);
        var historyChars = context.ConversationHistory.Sum(h => h.Content.Length);

        int Estimate() => (baseChars + memoryChars + historyChars) / charsPerToken;

        if (Estimate() <= budget)
        {
            return context;
        }

        // Strategy 1: drop low-relevance memories (keep highest-relevance first)
        if (context.RelevantMemories.Count > 0)
        {
            var ordered = context.RelevantMemories.OrderByDescending(m => m.Relevance).ToList();
            while (ordered.Count > 0 && Estimate() > budget)
            {
                var dropped = ordered[^1];
                memoryChars -= dropped.Content.Length;
                ordered.RemoveAt(ordered.Count - 1);
            }
            context.RelevantMemories = ordered;
            logger.LogDebug(
                "Context fit: reduced memories to {Count} for message {MessageId}",
                ordered.Count, context.MessageId);
        }

        if (Estimate() <= budget)
        {
            return context;
        }

        // Strategy 2: summarize older history (beyond MinRecentTurns)
        var minRecent = Math.Max(1, _options.MinRecentTurns);
        if (context.ConversationHistory.Count > minRecent)
        {
            await SummarizeHistoryAsync(context, minRecent, cancellationToken);
            historyChars = context.ConversationHistory.Sum(h => h.Content.Length);
        }

        if (Estimate() <= budget)
        {
            return context;
        }

        // Strategy 3: truncate — drop oldest remaining history messages
        var history = context.ConversationHistory.ToList();
        while (history.Count > 1 && Estimate() > budget)
        {
            historyChars -= history[0].Content.Length;
            history.RemoveAt(0);
        }
        context.ConversationHistory = history;

        logger.LogWarning(
            "Context window truncated for message {MessageId}: {HistoryCount} history, {MemoryCount} memories",
            context.MessageId, context.ConversationHistory.Count, context.RelevantMemories.Count);

        return context;
    }

    private async Task SummarizeHistoryAsync(
        ThinkingContext context,
        int minRecent,
        CancellationToken cancellationToken)
    {
        var history = context.ConversationHistory.ToList();
        var olderCount = history.Count - minRecent;
        if (olderCount <= 0) return;

        var older = history.Take(olderCount).ToList();
        var recent = history.Skip(olderCount).ToList();

        var summaryText = await GenerateSummaryAsync(older, cancellationToken);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var summaryMessage = new Message
        {
            Id = Guid.NewGuid(),
            Role = MessageRole.Summary,
            Content = summaryText,
            Channel = Enum.TryParse<MessageChannel>(context.Channel, true, out var ch)
                ? ch
                : MessageChannel.Cli,
            ConversationId = context.ConversationId,
            IsComplete = true
        };
        db.Add(summaryMessage);
        await db.SaveChangesAsync(cancellationToken);

        var condensed = new List<HistoryMessage>(recent.Count + 1)
        {
            new("System", $"[summary of earlier conversation] {summaryText}", older[0].Timestamp)
        };
        condensed.AddRange(recent);
        context.ConversationHistory = condensed;
    }

    private async Task<string> GenerateSummaryAsync(
        IReadOnlyList<HistoryMessage> older,
        CancellationToken cancellationToken)
    {
        var transcript = string.Join("\n", older.Select(m => $"{m.Role}: {m.Content}"));
        var prompt = new List<ChatMessage>
        {
            new(ChatRole.System, "Summarize the following conversation turns in under 200 words. Preserve user intent, decisions, and outstanding questions."),
            new(ChatRole.User, transcript)
        };

        try
        {
            var response = await summarizer.GetResponseAsync(prompt, cancellationToken: cancellationToken);
            return response.Text;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Summary generation failed; using naive fallback");
            return $"Earlier exchange covered: {older.Count} messages (summary unavailable).";
        }
    }
}
