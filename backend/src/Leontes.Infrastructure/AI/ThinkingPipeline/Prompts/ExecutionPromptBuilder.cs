using System.Text;
using Leontes.Domain.ThinkingPipeline;
using Microsoft.Extensions.AI;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Prompts;

internal static class ExecutionPromptBuilder
{
    public static IList<ChatMessage> Build(ThinkingContext context, string personaInstructions)
    {
        var messages = new List<ChatMessage>();

        messages.Add(new ChatMessage(ChatRole.System, BuildSystemPrompt(context, personaInstructions)));

        // Include conversation history
        foreach (var historyMsg in context.ConversationHistory)
        {
            var role = MapRole(historyMsg.Role);
            messages.Add(new ChatMessage(role, historyMsg.Content));
        }

        messages.Add(new ChatMessage(ChatRole.User, context.UserContent));

        return messages;
    }

    private static ChatRole MapRole(string role) => role.ToLowerInvariant() switch
    {
        "assistant" => ChatRole.Assistant,
        "system" => ChatRole.System,
        _ => ChatRole.User
    };

    private static string BuildSystemPrompt(ThinkingContext context, string personaInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine(personaInstructions);

        if (context.Plan is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Execution Plan");
            sb.AppendLine(context.Plan);
        }

        if (context.RelevantMemories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Relevant Context");
            foreach (var memory in context.RelevantMemories)
            {
                sb.AppendLine($"- {memory.Content}");
            }
        }

        if (context.ResolvedEntities.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Entity Context");
            foreach (var entity in context.ResolvedEntities)
            {
                sb.AppendLine($"- \"{entity.Mention}\" refers to {entity.ResolvedName} ({entity.EntityType})");
            }
        }

        if (context.HumanInputResponse is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"## User Clarification");
            sb.AppendLine(context.HumanInputResponse);
        }

        if (!string.IsNullOrWhiteSpace(context.ScreenState))
        {
            sb.AppendLine();
            sb.AppendLine("## Current Screen State");
            sb.AppendLine("The user is looking at:");
            sb.AppendLine(context.ScreenState);
            sb.AppendLine("Use this context to answer questions about what is visible on screen.");
        }

        return sb.ToString();
    }
}
