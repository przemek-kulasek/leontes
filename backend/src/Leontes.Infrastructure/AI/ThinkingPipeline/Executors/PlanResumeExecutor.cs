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

        thinkingContext.RequiresHumanInput = false;

        if (string.IsNullOrWhiteSpace(message))
        {
            // Timeout fired with no user response — proceed with best-effort answer
            thinkingContext.HumanInputResponse = null;
            thinkingContext.Plan ??= "Answer the user's question using only the information already in context. If the answer is not available, say so honestly and do not ask any follow-up questions.";
        }
        else
        {
            thinkingContext.HumanInputResponse = message;
            thinkingContext.Plan ??= $"Respond to user query using clarification provided: {message}";
        }

        await context.AddEventAsync(
            new ProgressEvent("PlanResume", "Clarification received, continuing execution", 0.55),
            cancellationToken);

        // Auto-sent to Execute via edge
        return thinkingContext;
    }
}
