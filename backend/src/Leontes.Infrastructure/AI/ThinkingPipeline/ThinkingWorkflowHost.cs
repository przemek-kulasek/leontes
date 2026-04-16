using Leontes.Application.ProactiveCommunication;
using Leontes.Domain.ThinkingPipeline;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

public sealed class ThinkingWorkflowHost(
    Workflow workflow,
    CheckpointManager checkpointManager,
    IWorkflowEventBridge eventBridge,
    IWorkflowSessionManager sessionManager,
    ILogger<ThinkingWorkflowHost> logger)
{
    public async Task<ThinkingContext> ProcessAsync(
        ThinkingContext input,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting thinking pipeline for message {MessageId} in conversation {ConversationId}",
            input.MessageId, input.ConversationId);

        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow, input, checkpointManager, cancellationToken: cancellationToken);

        sessionManager.SetActiveRun(run);

        try
        {
            ThinkingContext? result = null;

            await foreach (var evt in run.WatchStreamAsync(cancellationToken))
            {
                // Bridge all events to connected clients (feature 65)
                await eventBridge.PublishEventAsync(evt, cancellationToken);

                if (evt is WorkflowOutputEvent outputEvt &&
                    outputEvt.Data is ThinkingContext output)
                {
                    result = output;
                }
            }

            if (result is null)
            {
                throw new InvalidOperationException(
                    $"Thinking pipeline completed without producing output for message {input.MessageId}");
            }

            logger.LogInformation(
                "Thinking pipeline completed for message {MessageId}: complete={IsComplete}",
                input.MessageId, result.IsComplete);

            return result;
        }
        finally
        {
            sessionManager.ClearActiveRun();
        }
    }
}
