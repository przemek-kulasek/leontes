using Leontes.Application;
using Leontes.Application.Configuration;
using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Executors;

internal sealed class EnrichExecutor(
    IServiceScopeFactory scopeFactory,
    IMemoryStore memoryStore,
    ISynapseGraph synapseGraph,
    IOptions<ThinkingPipelineOptions> options,
    ILogger<EnrichExecutor> logger)
    : Executor<ThinkingContext, ThinkingContext>("Enrich")
{
    public override async ValueTask<ThinkingContext> HandleAsync(
        ThinkingContext message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await context.AddEventAsync(
            new ProgressEvent("Enrich", "Searching for relevant context...", 0.2),
            cancellationToken);

        var config = options.Value;

        // Load conversation history
        try
        {
            message.ConversationHistory = await LoadHistoryAsync(
                message.ConversationId, config.MaxConversationHistoryMessages, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to load conversation history for {ConversationId}",
                message.ConversationId);
        }

        // Search episodic/semantic memory
        try
        {
            message.RelevantMemories = await memoryStore.SearchAsync(
                message.UserContent, config.MaxRelevantMemories, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Memory search failed, continuing without memories");
        }

        // Resolve entity mentions via Synapse Graph
        var resolved = new List<ResolvedEntity>();
        foreach (var entity in message.ExtractedEntities)
        {
            try
            {
                var result = await synapseGraph.ResolveAsync(entity, cancellationToken);
                if (result is not null)
                    resolved.Add(result);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to resolve entity '{Entity}'", entity);
            }
        }
        message.ResolvedEntities = resolved;

        await context.AddEventAsync(
            new ProgressEvent("Enrich",
                $"Found {message.RelevantMemories.Count} memories, " +
                $"resolved {message.ResolvedEntities.Count} entities",
                0.4),
            cancellationToken);

        logger.LogDebug(
            "Enriched message {MessageId}: {MemoryCount} memories, {HistoryCount} history messages, {EntityCount} resolved entities",
            message.MessageId, message.RelevantMemories.Count,
            message.ConversationHistory.Count, message.ResolvedEntities.Count);

        return message;
    }

    private async Task<IReadOnlyList<HistoryMessage>> LoadHistoryAsync(
        Guid conversationId, int limit, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var messages = await db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.IsComplete)
            .OrderByDescending(m => m.Created)
            .Take(limit)
            .Select(m => new HistoryMessage(m.Role.ToString(), m.Content, m.Created))
            .ToListAsync(cancellationToken);

        // Return in chronological order
        messages.Reverse();
        return messages;
    }
}
