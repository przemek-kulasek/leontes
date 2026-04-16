using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Executors;

internal sealed class PerceiveExecutor(
    ILogger<PerceiveExecutor> logger)
    : Executor<ThinkingContext, ThinkingContext>("Perceive")
{
    public override async ValueTask<ThinkingContext> HandleAsync(
        ThinkingContext message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await context.AddEventAsync(
            new ProgressEvent("Perceive", "Parsing intent and entities...", 0.0),
            cancellationToken);

        try
        {
            message.ExtractedEntities = EntityExtractor.Extract(message.UserContent);
            message.Intent = IntentClassifier.Classify(message.UserContent);
            message.Urgency = UrgencyDetector.Detect(message.UserContent, message.Channel);

            logger.LogDebug(
                "Perceived intent {Intent} with {EntityCount} entities, urgency {Urgency}",
                message.Intent, message.ExtractedEntities.Count, message.Urgency);
        }
        catch (Exception ex)
        {
            // Perceive is enrichment — degrade gracefully
            logger.LogWarning(ex,
                "Perception failed for message {MessageId}, continuing with defaults",
                message.MessageId);
        }

        await context.AddEventAsync(
            new ProgressEvent("Perceive", "Perception complete", 0.1),
            cancellationToken);

        return message;
    }
}
