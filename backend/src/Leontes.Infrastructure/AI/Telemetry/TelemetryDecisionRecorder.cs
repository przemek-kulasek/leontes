using Leontes.Application.Telemetry;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Entities;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Sync shim over <see cref="ITelemetryCollector"/> for executors that record decisions
/// without an available async context. Delegates are non-blocking — the collector
/// enqueues on a channel.
/// </summary>
internal sealed class TelemetryDecisionRecorder(ITelemetryCollector collector) : IDecisionRecorder
{
    public void Record(
        Guid requestId,
        string stageName,
        string decisionType,
        string chosen,
        string rationale,
        IReadOnlyList<DecisionCandidate>? candidates = null)
    {
        _ = collector.RecordDecisionAsync(
            requestId, stageName, decisionType,
            question: decisionType,
            chosen: chosen,
            rationale: rationale,
            candidates: candidates,
            cancellationToken: CancellationToken.None);
    }
}
