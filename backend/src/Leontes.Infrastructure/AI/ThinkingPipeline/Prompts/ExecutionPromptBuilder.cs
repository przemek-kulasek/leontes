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

        // Screen state goes first — before memories and history — so the model treats
        // it as the authoritative ground truth rather than letting older context win.
        if (!string.IsNullOrWhiteSpace(context.ScreenState))
        {
            sb.AppendLine();
            sb.AppendLine("## Current Screen State (live UI Automation capture)");
            sb.AppendLine("This is a REAL-TIME capture of the user's screen taken RIGHT NOW via Windows UI Automation. It supersedes anything mentioned about the screen earlier in this conversation.");
            sb.AppendLine("RULE: When asked about what is on screen, report ONLY the values you see below. Do not use screen content from earlier in the conversation. Do not guess or paraphrase.");
            sb.AppendLine();
            sb.AppendLine(context.ScreenState);
        }

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

        return sb.ToString();
    }
}
