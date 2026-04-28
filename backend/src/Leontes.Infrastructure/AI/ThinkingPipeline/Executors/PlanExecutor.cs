using Leontes.Application.Configuration;
using Leontes.Application.CostControl;
using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;
using Leontes.Infrastructure.AI.ThinkingPipeline.Prompts;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Executors;

/// <summary>
/// Strategy formulation stage. Uses the Large LLM to create an approach plan.
/// </summary>
[SendsMessage(typeof(ThinkingContext))]
internal sealed class PlanExecutor(
    [FromKeyedServices("Large")] IChatClient chatClient,
    PersonaInstructions persona,
    ITokenMeter tokenMeter,
    IThrottleEngine throttleEngine,
    IDecisionRecorder decisionRecorder,
    IOptions<PersonaOptions> personaOptions,
    IOptions<AiProviderOptions> aiProviderOptions,
    ILogger<PlanExecutor> logger)
    : Executor<ThinkingContext>("Plan")
{

    public override async ValueTask HandleAsync(
        ThinkingContext message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await context.AddEventAsync(
            new ProgressEvent("Plan", "Determining approach...", 0.5),
            cancellationToken);

        var throttle = await throttleEngine.EvaluateAsync(
            CostControlFeatures.Chat, "Plan", cancellationToken);

        if (!throttle.Allowed)
        {
            var denial = throttle.DenialReason
                ?? "I'm pausing this request to stay within today's token budget. Please try again later.";

            message.Plan = string.Empty;
            message.SelectedTools = [];
            message.Response = denial;
            message.IsComplete = true;

            decisionRecorder.Record(message.MessageId, "Plan", "BudgetThrottle",
                "Denied", denial);

            logger.LogWarning(
                "Plan stage throttled for message {MessageId}: {Reason}",
                message.MessageId, denial);

            await context.AddEventAsync(
                new ProgressEvent("Plan", "Throttled by budget policy", 0.55),
                cancellationToken);

            await context.SendMessageAsync(message, cancellationToken: cancellationToken);
            return;
        }

        if (throttle.DelayBefore is { } delay && delay > TimeSpan.Zero)
        {
            logger.LogInformation(
                "Plan stage delayed {DelayMs}ms by budget policy for message {MessageId}",
                delay.TotalMilliseconds, message.MessageId);
            await Task.Delay(delay, cancellationToken);
        }

        var stageSettings = personaOptions.Value.StageSettings
            .GetValueOrDefault("Plan", new StageSettings { ModelTier = "Large", Temperature = 0.2f });

        var chatOptions = new ChatOptions { Temperature = stageSettings.Temperature };
        var planningMessages = PlanningPromptBuilder.Build(
            message,
            persona.Instructions,
            personaOptions.Value.ConfidenceThreshold,
            personaOptions.Value.ProactivityLevel);

        string planText;
        try
        {
            var response = await chatClient.GetResponseAsync(
                planningMessages, chatOptions, cancellationToken);
            planText = response.Text;
            var modelId = aiProviderOptions.Value.Models.GetValueOrDefault("Large")?.ModelId ?? "Large";
            tokenMeter.Record(
                CostControlFeatures.Chat,
                "Plan",
                modelId,
                (int)(response.Usage?.InputTokenCount ?? 0),
                (int)(response.Usage?.OutputTokenCount ?? 0));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Plan LLM call failed for message {MessageId}; degrading to direct-response mode",
                message.MessageId);

            message.Plan = string.Empty;
            message.SelectedTools = [];
            decisionRecorder.Record(message.MessageId, "Plan", "ExecutionMode",
                "DirectResponse", "LLM unavailable — fell back to direct response");

            await context.AddEventAsync(
                new ProgressEvent("Plan", "Plan stage degraded", 0.55),
                cancellationToken);

            await context.SendMessageAsync(message, cancellationToken: cancellationToken);
            return;
        }

        message.Plan = planText;
        message.SelectedTools = ToolSelector.FromPlan(planText);

        decisionRecorder.Record(message.MessageId, "Plan", "ToolSelection",
            chosen: string.Join(", ", message.SelectedTools.DefaultIfEmpty("(none)")),
            rationale: "Tools extracted from generated plan");

        logger.LogDebug(
            "Plan created for message {MessageId}: {ToolCount} tools selected",
            message.MessageId, message.SelectedTools.Count);

        await context.AddEventAsync(
            new ProgressEvent("Plan", "Plan ready", 0.55),
            cancellationToken);

        // Route to Execute
        await context.SendMessageAsync(message, cancellationToken: cancellationToken);
    }
}
