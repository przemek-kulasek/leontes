using Leontes.Domain.Enums;

namespace Leontes.Domain.Entities;

public sealed class PipelineTrace : Entity
{
    public required Guid RequestId { get; init; }
    public required Guid ConversationId { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public PipelineOutcome Outcome { get; set; } = PipelineOutcome.Success;
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public double? ConfidenceOverall { get; set; }
    public double? ConfidenceMemorySupport { get; set; }
    public double? ConfidenceGraphSupport { get; set; }
    public double? ConfidenceConversationClarity { get; set; }
    public double? ConfidenceToolReliability { get; set; }

    public List<StageTrace> Stages { get; init; } = [];
}
