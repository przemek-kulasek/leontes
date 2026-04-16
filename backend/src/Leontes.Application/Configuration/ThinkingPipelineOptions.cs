namespace Leontes.Application.Configuration;

public sealed class ThinkingPipelineOptions
{
    public const string SectionName = "ThinkingPipeline";

    public int DefaultQuestionTimeoutMinutes { get; set; } = 5;
    public int MaxConversationHistoryMessages { get; set; } = 20;
    public int MaxRelevantMemories { get; set; } = 10;
    public double MemoryRelevanceThreshold { get; set; } = 0.7;
}
