using Leontes.Domain.Enums;

namespace Leontes.Domain.Entities;

public sealed class StageTrace : Entity
{
    public required Guid PipelineTraceId { get; set; }
    public required string StageName { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public StageOutcome Outcome { get; set; } = StageOutcome.Success;
    public string? ErrorMessage { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }

    public PipelineTrace? PipelineTrace { get; set; }
    public List<DecisionRecord> Decisions { get; init; } = [];
}
