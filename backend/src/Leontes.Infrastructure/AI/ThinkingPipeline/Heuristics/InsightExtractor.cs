using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

internal static class InsightExtractor
{
    public static IReadOnlyList<string> Extract(ThinkingContext context)
    {
        var insights = new List<string>();

        // Record tool usage patterns
        foreach (var tool in context.ToolResults)
        {
            if (tool.Success)
                insights.Add($"Successfully used tool '{tool.ToolName}' for intent '{context.Intent}'");
        }

        // Record entity resolution successes
        if (context.ResolvedEntities.Count > 0)
        {
            insights.Add(
                $"Resolved {context.ResolvedEntities.Count} entities: " +
                string.Join(", ", context.ResolvedEntities.Select(e => $"{e.Mention} -> {e.ResolvedName}")));
        }

        return insights;
    }
}
