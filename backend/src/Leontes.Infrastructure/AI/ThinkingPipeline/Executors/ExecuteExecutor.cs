using System.Text;
using Leontes.Application.Configuration;
using Leontes.Application.CostControl;
using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Prompts;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Executors;

/// <summary>
/// Response generation stage. Uses the Large LLM with streaming to produce the response.
/// Emits token events for real-time SSE delivery.
/// </summary>
internal sealed class ExecuteExecutor(
    [FromKeyedServices("Large")] IChatClient chatClient,
    PersonaInstructions persona,
    IEnumerable<AITool> tools,
    ITokenMeter tokenMeter,
    IOptions<PersonaOptions> personaOptions,
    IOptions<AiProviderOptions> aiProviderOptions,
    ILogger<ExecuteExecutor> logger)
    : Executor<ThinkingContext, ThinkingContext>("Execute")
{
    public override async ValueTask<ThinkingContext> HandleAsync(
        ThinkingContext message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        await context.AddEventAsync(
            new ProgressEvent("Execute", "Generating response...", 0.6),
            cancellationToken);

        var stageSettings = personaOptions.Value.StageSettings
            .GetValueOrDefault("Execute", new StageSettings { ModelTier = "Large", Temperature = 0.5f });

        var chatOptions = new ChatOptions
        {
            Temperature = stageSettings.Temperature,
            Tools = [.. tools]
        };

        var executionMessages = ExecutionPromptBuilder.Build(
            message, persona.Instructions);

        var responseBuilder = new StringBuilder();
        var inputTokens = 0L;
        var outputTokens = 0L;

        try
        {
            var streamingResponse = chatClient.GetStreamingResponseAsync(
                executionMessages, chatOptions, cancellationToken);

            await foreach (var update in streamingResponse)
            {
                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case TextContent text when !string.IsNullOrEmpty(text.Text):
                            responseBuilder.Append(text.Text);
                            await context.AddEventAsync(
                                new TokenStreamEvent(text.Text),
                                cancellationToken);
                            break;

                        case UsageContent usage:
                            inputTokens += usage.Details.InputTokenCount ?? 0;
                            outputTokens += usage.Details.OutputTokenCount ?? 0;
                            break;
                    }
                }
            }

            message.Response = responseBuilder.ToString();
            message.IsComplete = true;

            var modelId = aiProviderOptions.Value.Models.GetValueOrDefault("Large")?.ModelId ?? "Large";
            tokenMeter.Record(
                CostControlFeatures.Chat,
                "Execute",
                modelId,
                (int)inputTokens,
                (int)outputTokens);
        }
        catch (OperationCanceledException)
        {
            // Client disconnect — save partial response
            message.Response = responseBuilder.ToString();
            message.IsComplete = false;
            logger.LogWarning(
                "Execute interrupted for message {MessageId}, partial response length {Length}",
                message.MessageId, responseBuilder.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Execute failed for message {MessageId}; delivering partial+error response",
                message.MessageId);

            var partial = responseBuilder.ToString();
            var apology = partial.Length > 0
                ? partial + "\n\n[I ran into a problem finishing this response. Please try again shortly.]"
                : "I'm having trouble reaching the AI provider right now. Please try again shortly.";

            message.Response = apology;
            message.IsComplete = responseBuilder.Length == 0;
        }

        await context.AddEventAsync(
            new ProgressEvent("Execute", "Response complete", 1.0),
            cancellationToken);

        logger.LogDebug(
            "Execute completed for message {MessageId}: {ResponseLength} chars, complete={IsComplete}",
            message.MessageId, message.Response?.Length ?? 0, message.IsComplete);

        return message;
    }
}
