using Leontes.Application.Configuration;
using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Derives a <see cref="ConfidenceScore"/> from the populated <see cref="ThinkingContext"/>.
/// Pure function — no I/O — safe to call from any stage.
/// </summary>
public static class ConfidenceCalculator
{
    public static ConfidenceScore Calculate(ThinkingContext context, ThinkingPipelineOptions pipelineOptions)
    {
        var memory = ScoreMemorySupport(context, pipelineOptions);
        var graph = ScoreGraphSupport(context);
        var clarity = ScoreConversationClarity(context);
        var tools = ScoreToolReliability(context);

        var overall = Clamp01((memory * 0.25) + (graph * 0.25) + (clarity * 0.3) + (tools * 0.2));

        return new ConfidenceScore(overall,
            new ConfidenceBreakdown(memory, graph, clarity, tools));
    }

    private static double ScoreMemorySupport(ThinkingContext context, ThinkingPipelineOptions pipelineOptions)
    {
        if (context.RelevantMemories.Count == 0)
            return 0.0;

        var threshold = pipelineOptions.MemoryRelevanceThreshold;
        var relevant = context.RelevantMemories.Where(m => m.Relevance >= threshold).ToList();
        if (relevant.Count == 0)
            return 0.2;

        var avg = relevant.Average(m => m.Relevance);
        var coverage = Math.Min(1.0, relevant.Count / 3.0);
        return Clamp01(avg * coverage);
    }

    private static double ScoreGraphSupport(ThinkingContext context)
    {
        if (context.ExtractedEntities.Count == 0)
            return 0.5;

        var resolved = context.ResolvedEntities.Count;
        return Clamp01(resolved / (double)context.ExtractedEntities.Count);
    }

    private static double ScoreConversationClarity(ThinkingContext context)
    {
        if (context.RequiresHumanInput)
            return 0.3;

        if (string.IsNullOrWhiteSpace(context.Intent))
            return 0.4;

        var trimmed = context.UserContent.Trim();
        if (trimmed.EndsWith('?'))
            return 0.7;

        return trimmed.Length < 10 ? 0.5 : 0.85;
    }

    private static double ScoreToolReliability(ThinkingContext context)
    {
        if (context.ToolResults.Count == 0)
            return 0.8;

        var successes = context.ToolResults.Count(t => t.Success);
        return Clamp01(successes / (double)context.ToolResults.Count);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
