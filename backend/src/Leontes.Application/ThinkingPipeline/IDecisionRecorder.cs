using Leontes.Domain.Entities;

namespace Leontes.Application.ThinkingPipeline;

/// <summary>
/// Records a decision made by a pipeline stage. Implementations forward to
/// <see cref="Leontes.Application.Telemetry.ITelemetryCollector"/>.
/// </summary>
public interface IDecisionRecorder
{
    void Record(
        Guid requestId,
        string stageName,
        string decisionType,
        string chosen,
        string rationale,
        IReadOnlyList<DecisionCandidate>? candidates = null);
}
