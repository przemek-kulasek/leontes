using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Domain.ThinkingPipeline;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Executors;

/// <summary>
/// Receives the human response from the QuestionPort, restores the ThinkingContext
/// from shared workflow state, and forwards to Execute.
/// </summary>
internal sealed class PlanResumeExecutor(
    ILogger<PlanResumeExecutor> logger)
    : Executor<string, ThinkingContext>("PlanResume")
{
    private const string ContextStateKey = "PlanThinkingContext";

    public override async ValueTask<ThinkingContext> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Received human clarification response, resuming pipeline");

        // Restore the ThinkingContext that Plan saved before pausing
        var thinkingContext = await context.ReadStateAsync<ThinkingContext>(
            ContextStateKey, "shared", cancellationToken);

        if (thinkingContext is null)
        {
            throw new InvalidOperationException(
                "ThinkingContext not found in shared state. " +
                "PlanExecutor should have saved it before requesting clarification.");
        }

        thinkingContext.HumanInputResponse = message;
        thinkingContext.RequiresHumanInput = false;

        // Create a default plan incorporating the clarification
        thinkingContext.Plan ??= $"Respond to user query with clarification: {message}";

        await context.AddEventAsync(
            new ProgressEvent("PlanResume", "Clarification received, continuing execution", 0.55),
            cancellationToken);

        // Auto-sent to Execute via edge
        return thinkingContext;
    }
}
