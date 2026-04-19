using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Application.Vision;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Executors;

internal sealed class PerceiveExecutor(
    IUITreeWalker uiTreeWalker,
    ITreeSerializer treeSerializer,
    IOptions<VisionOptions> visionOptions,
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

        await CaptureScreenStateAsync(message, cancellationToken);

        await context.AddEventAsync(
            new ProgressEvent("Perceive", "Perception complete", 0.1),
            cancellationToken);

        return message;
    }

    private async Task CaptureScreenStateAsync(ThinkingContext message, CancellationToken cancellationToken)
    {
        var options = visionOptions.Value;
        if (!options.Enabled)
            return;

        if (options.RequireExplicitRequest && !ScreenIntentClassifier.RequiresScreenContext(message.UserContent))
            return;

        try
        {
            var walkerOptions = new TreeWalkerOptions(MaxDepth: options.MaxTreeDepth);
            var tree = await uiTreeWalker.CaptureFocusedWindowTreeAsync(walkerOptions, cancellationToken);
            if (tree is null)
            {
                logger.LogDebug("Vision: no tree captured for message {MessageId}.", message.MessageId);
                return;
            }

            var serializerOptions = new TreeSerializerOptions(
                MaxTokenEstimate: options.MaxTokenEstimate,
                IncludeBounds: options.IncludeBounds);

            message.ScreenState = treeSerializer.Serialize(tree, serializerOptions);

            logger.LogInformation(
                "Vision: captured {Chars} chars of screen state for message {MessageId}.",
                message.ScreenState.Length, message.MessageId);
            logger.LogDebug("Vision tree:\n{Tree}", message.ScreenState);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Vision: capture failed for message {MessageId}, continuing without screen state.",
                message.MessageId);
        }
    }
}
