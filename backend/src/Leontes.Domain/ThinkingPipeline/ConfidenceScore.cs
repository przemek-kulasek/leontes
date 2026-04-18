namespace Leontes.Domain.ThinkingPipeline;

public sealed record ConfidenceScore(double Overall, ConfidenceBreakdown Breakdown);

public sealed record ConfidenceBreakdown(
    double MemorySupport,
    double GraphSupport,
    double ConversationClarity,
    double ToolReliability);
