using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Executors;

/// <summary>
/// Memory consolidation and graph update stage. Stores episodic memories
/// and extracts insights from the completed interaction.
/// </summary>
internal sealed class ReflectExecutor(
    IMemoryStore memoryStore,
    ISynapseGraph synapseGraph,
    IDecisionRecorder decisionRecorder,
    ILogger<ReflectExecutor> logger)
    : Executor<ThinkingContext, ThinkingContext>("Reflect")
{
    public override async ValueTask<ThinkingContext> HandleAsync(
        ThinkingContext message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (!message.IsComplete)
        {
            logger.LogWarning(
                "Skipping reflection for incomplete response on message {MessageId}",
                message.MessageId);
            return message;
        }

        await context.AddEventAsync(
            new ProgressEvent("Reflect", "Consolidating insights...", 0.9),
            cancellationToken);

        // Extract insights
        try
        {
            message.NewInsights = InsightExtractor.Extract(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Insight extraction failed for message {MessageId}", message.MessageId);
        }

        // Store episodic memory
        try
        {
            await memoryStore.StoreEpisodicAsync(
                message.ConversationId,
                message.UserContent,
                message.Response!,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to store episodic memory for message {MessageId}",
                message.MessageId);
        }

        // Update Synapse Graph relationships
        foreach (var update in message.GraphUpdates)
        {
            try
            {
                await synapseGraph.AddRelationshipAsync(
                    update.EntityId, update.RelationType, update.RelatedEntityId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex,
                    "Failed to update graph relationship for entity {EntityId}",
                    update.EntityId);
            }
        }

        if (message.NewInsights.Count > 0)
        {
            decisionRecorder.Record("Reflect", "InsightsExtracted",
                string.Join("; ", message.NewInsights));

            await context.AddEventAsync(
                new InsightEvent(
                    string.Join("; ", message.NewInsights),
                    "Reflection"),
                cancellationToken);
        }

        logger.LogDebug(
            "Reflection completed for message {MessageId}: {InsightCount} insights",
            message.MessageId, message.NewInsights.Count);

        // This is the final executor — auto-yields as workflow output
        return message;
    }
}
