namespace Leontes.Domain.Entities;

public sealed class DecisionRecord : Entity
{
    public required Guid StageTraceId { get; set; }
    public required string DecisionType { get; init; }
    public required string Question { get; init; }
    public required string Chosen { get; init; }
    public required string Rationale { get; init; }

    /// <summary>
    /// Serialized as JSONB. Each candidate captures a scored alternative considered at this decision point.
    /// </summary>
    public IReadOnlyList<DecisionCandidate> Candidates { get; set; } = [];

    public StageTrace? StageTrace { get; set; }
}

public sealed record DecisionCandidate(string Name, double Score, string Reason);
