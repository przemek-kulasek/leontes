using Leontes.Application.Configuration;
using Leontes.Application.ProactiveCommunication;
using Leontes.Application.Telemetry;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.Telemetry;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

public sealed class ThinkingWorkflowHost(
    Workflow workflow,
    CheckpointManager checkpointManager,
    IWorkflowEventBridge eventBridge,
    IWorkflowSessionManager sessionManager,
    ITelemetryCollector telemetry,
    IOptions<ThinkingPipelineOptions> pipelineOptions,
    ILogger<ThinkingWorkflowHost> logger)
{
    public async Task<ThinkingContext> ProcessAsync(
        ThinkingContext input,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting thinking pipeline for message {MessageId} in conversation {ConversationId}",
            input.MessageId, input.ConversationId);

        // Logging scope — every log entry inside this block is enriched with RequestId,
        // correlating pipeline logs with the PipelineTrace rows written by telemetry.
        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = input.MessageId
        });

        var pipelineStartedAt = DateTime.UtcNow;
        await telemetry.RecordPipelineStartAsync(
            input.MessageId, input.ConversationId, pipelineStartedAt, cancellationToken);

        var failedStages = new HashSet<string>();
        var pipelineOutcome = PipelineOutcome.Success;

        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow, input, checkpointManager, cancellationToken: cancellationToken);

        sessionManager.SetActiveRun(run);

        try
        {
            ThinkingContext? result = null;

            await foreach (var evt in run.WatchStreamAsync(cancellationToken))
            {
                await eventBridge.PublishEventAsync(evt, cancellationToken);

                switch (evt)
                {
                    case ExecutorInvokedEvent invoked when invoked.ExecutorId is { } id:
                        await telemetry.RecordStageStartAsync(
                            input.MessageId, id, DateTime.UtcNow, cancellationToken);
                        break;

                    case ExecutorFailedEvent failed when failed.ExecutorId is { } id:
                        failedStages.Add(id);
                        pipelineOutcome = PipelineOutcome.Failed;
                        // Token counts are 0 here — per-stage token metering is wired in feature 100
                        // (Cost Control & Budget Management) via ITokenMeter around IChatClient.
                        await telemetry.RecordStageCompleteAsync(
                            input.MessageId, id, StageOutcome.Failed,
                            inputTokens: 0, outputTokens: 0,
                            errorMessage: failed.Data?.ToString(),
                            cancellationToken);
                        break;

                    case ExecutorCompletedEvent completed when completed.ExecutorId is { } id
                        && !failedStages.Contains(id):
                        // Token counts are 0 here — per-stage token metering is wired in feature 100
                        // (Cost Control & Budget Management) via ITokenMeter around IChatClient.
                        await telemetry.RecordStageCompleteAsync(
                            input.MessageId, id, StageOutcome.Success,
                            inputTokens: 0, outputTokens: 0, errorMessage: null, cancellationToken);
                        break;

                    case WorkflowOutputEvent outputEvt when outputEvt.Data is ThinkingContext output:
                        result = output;
                        break;
                }
            }

            if (result is null)
            {
                await telemetry.RecordPipelineCompleteAsync(
                    input.MessageId, PipelineOutcome.Failed, confidence: null, cancellationToken);
                throw new InvalidOperationException(
                    $"Thinking pipeline completed without producing output for message {input.MessageId}");
            }

            result.Confidence = ConfidenceCalculator.Calculate(result, pipelineOptions.Value);

            if (pipelineOutcome != PipelineOutcome.Failed)
                pipelineOutcome = result.IsComplete
                    ? PipelineOutcome.Success
                    : PipelineOutcome.PartialSuccess;

            await telemetry.RecordPipelineCompleteAsync(
                input.MessageId, pipelineOutcome, result.Confidence, cancellationToken);

            logger.LogInformation(
                "Thinking pipeline completed for message {MessageId}: complete={IsComplete} confidence={Confidence:F2}",
                input.MessageId, result.IsComplete, result.Confidence.Overall);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await telemetry.RecordPipelineCompleteAsync(
                input.MessageId, PipelineOutcome.Failed, confidence: null, CancellationToken.None);
            throw;
        }
        finally
        {
            sessionManager.ClearActiveRun();
        }
    }
}
