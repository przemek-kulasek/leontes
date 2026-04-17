using Leontes.Application.Configuration;
using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Application.ProactiveCommunication.Requests;
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
/// Can pause the workflow via RequestPort if clarification is needed.
/// </summary>
[SendsMessage(typeof(ThinkingContext))]
[SendsMessage(typeof(QuestionRequest))]
internal sealed class PlanExecutor(
    [FromKeyedServices("Large")] IChatClient chatClient,
    PersonaInstructions persona,
    ITokenMeter tokenMeter,
    IDecisionRecorder decisionRecorder,
    IOptions<PersonaOptions> personaOptions,
    IOptions<ThinkingPipelineOptions> pipelineOptions,
    ILogger<PlanExecutor> logger)
    : Executor<ThinkingContext>("Plan")
{
    private const string ContextStateKey = "PlanThinkingContext";
    private const string NeedsClarificationPrefix = "[NEEDS_CLARIFICATION]";

    public override async ValueTask HandleAsync(
        ThinkingContext message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await context.AddEventAsync(
            new ProgressEvent("Plan", "Determining approach...", 0.5),
            cancellationToken);

        var stageSettings = personaOptions.Value.StageSettings
            .GetValueOrDefault("Plan", new StageSettings { ModelTier = "Large", Temperature = 0.2f });

        var chatOptions = new ChatOptions { Temperature = stageSettings.Temperature };
        var planningMessages = PlanningPromptBuilder.Build(
            message,
            persona.Instructions,
            personaOptions.Value.ConfidenceThreshold,
            personaOptions.Value.ProactivityLevel);

        var response = await chatClient.GetResponseAsync(
            planningMessages, chatOptions, cancellationToken);

        var planText = response.Text;
        tokenMeter.Record("Plan",
            (int)(response.Usage?.InputTokenCount ?? 0),
            (int)(response.Usage?.OutputTokenCount ?? 0));

        // Check if the LLM requests clarification
        if (planText.StartsWith(NeedsClarificationPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var question = planText[NeedsClarificationPrefix.Length..].Trim();
            message.RequiresHumanInput = true;
            message.HumanInputQuestion = question;

            decisionRecorder.Record("Plan", "RequestClarification", question);
            logger.LogInformation(
                "Plan stage requesting clarification for message {MessageId}: {Question}",
                message.MessageId, question);

            // Save context to workflow state so PlanResume can retrieve it
            await context.QueueStateUpdateAsync(ContextStateKey, message, "shared", cancellationToken);

            // Route to QuestionPort — workflow will pause for human response
            var questionRequest = new QuestionRequest(
                "Clarification needed",
                question,
                Options: null,
                Timeout: TimeSpan.FromMinutes(pipelineOptions.Value.DefaultQuestionTimeoutMinutes));

            await context.SendMessageAsync(questionRequest, cancellationToken: cancellationToken);
            return;
        }

        message.Plan = planText;
        message.SelectedTools = ToolSelector.FromPlan(planText);

        decisionRecorder.Record("Plan", "PlanCreated",
            $"Tools: [{string.Join(", ", message.SelectedTools)}]");

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
