using Leontes.Domain.Enums;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

internal static class UrgencyDetector
{
    private static readonly string[] CriticalKeywords =
        ["urgent", "emergency", "critical", "asap", "immediately", "right now"];

    // Multi-word phrases first to avoid "priority" matching before "low priority"
    private static readonly string[] LowKeywords =
        ["no rush", "low priority", "when you can", "whenever", "eventually"];

    private static readonly string[] HighKeywords =
        ["important", "high priority", "soon", "hurry", "quickly"];

    public static MessageUrgency Detect(string content, string channel)
    {
        if (string.IsNullOrWhiteSpace(content))
            return MessageUrgency.Normal;

        var lower = content.ToLowerInvariant();

        if (CriticalKeywords.Any(k => lower.Contains(k, StringComparison.Ordinal)))
            return MessageUrgency.Critical;

        // Check Low before High so "low priority" isn't caught by a High keyword
        if (LowKeywords.Any(k => lower.Contains(k, StringComparison.Ordinal)))
            return MessageUrgency.Low;

        if (HighKeywords.Any(k => lower.Contains(k, StringComparison.Ordinal)))
            return MessageUrgency.High;

        return MessageUrgency.Normal;
    }
}
