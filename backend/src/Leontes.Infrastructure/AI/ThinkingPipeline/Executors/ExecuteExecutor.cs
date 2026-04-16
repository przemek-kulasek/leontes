using System.Text;
using Leontes.Application.Configuration;
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

        try
        {
            var streamingResponse = chatClient.GetStreamingResponseAsync(
                executionMessages, chatOptions, cancellationToken);

            await foreach (var update in streamingResponse)
            {
                foreach (var content in update.Contents)
                {
                    if (content is TextContent text && !string.IsNullOrEmpty(text.Text))
                    {
                        responseBuilder.Append(text.Text);
                        await context.AddEventAsync(
                            new TokenStreamEvent(text.Text),
                            cancellationToken);
                    }
                }
            }

            message.Response = responseBuilder.ToString();
            message.IsComplete = true;

            // Token usage not reliably available on streaming updates;
            // metered at zero until ITokenMeter is properly wired (feature 100)
            tokenMeter.Record("Execute", 0, 0);
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

        await context.AddEventAsync(
            new ProgressEvent("Execute", "Response complete", 1.0),
            cancellationToken);

        logger.LogDebug(
            "Execute completed for message {MessageId}: {ResponseLength} chars, complete={IsComplete}",
            message.MessageId, message.Response?.Length ?? 0, message.IsComplete);

        return message;
    }
}
