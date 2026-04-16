using System.Text;
using Leontes.Domain.ThinkingPipeline;
using Microsoft.Extensions.AI;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Prompts;

internal static class PlanningPromptBuilder
{
    public static IList<ChatMessage> Build(ThinkingContext context, string personaInstructions)
    {
        var messages = new List<ChatMessage>();

        messages.Add(new ChatMessage(ChatRole.System, BuildSystemPrompt(personaInstructions)));

        // Include conversation history for multi-turn context
        foreach (var historyMsg in context.ConversationHistory)
        {
            var role = MapRole(historyMsg.Role);
            messages.Add(new ChatMessage(role, historyMsg.Content));
        }

        messages.Add(new ChatMessage(ChatRole.User, BuildPlanningUserPrompt(context)));

        return messages;
    }

    private static ChatRole MapRole(string role) => role.ToLowerInvariant() switch
    {
        "assistant" => ChatRole.Assistant,
        "system" => ChatRole.System,
        _ => ChatRole.User
    };

    private static string BuildSystemPrompt(string personaInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine(personaInstructions);
        sb.AppendLine();
        sb.AppendLine("## Planning Instructions");
        sb.AppendLine();
        sb.AppendLine("You are in the PLANNING stage. Your job is to analyze the user's message and create a brief plan for how to respond.");
        sb.AppendLine("- State what approach you will take (1-3 sentences)");
        sb.AppendLine("- If tools are needed, reference them as [tool:toolName]");
        sb.AppendLine("- If you need clarification from the user before you can respond well, start your plan with [NEEDS_CLARIFICATION] followed by the question");
        sb.AppendLine("- Keep the plan concise — it guides execution, not the user");
        return sb.ToString();
    }

    private static string BuildPlanningUserPrompt(ThinkingContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**Intent:** {context.Intent ?? "unknown"}");
        sb.AppendLine($"**Urgency:** {context.Urgency}");
        sb.AppendLine($"**Channel:** {context.Channel}");

        if (context.ExtractedEntities.Count > 0)
        {
            sb.AppendLine($"**Entities:** {string.Join(", ", context.ExtractedEntities)}");
        }

        if (context.RelevantMemories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Relevant memories:**");
            foreach (var memory in context.RelevantMemories)
            {
                sb.AppendLine($"- [{memory.Type}, relevance {memory.Relevance:F2}] {memory.Content}");
            }
        }

        if (context.ResolvedEntities.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Resolved entities:**");
            foreach (var entity in context.ResolvedEntities)
            {
                sb.AppendLine($"- \"{entity.Mention}\" → {entity.ResolvedName} ({entity.EntityType})");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"**User message:** {context.UserContent}");

        if (context.HumanInputResponse is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"**User clarification:** {context.HumanInputResponse}");
        }

        return sb.ToString();
    }
}
