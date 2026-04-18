using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Application.Telemetry;

/// <summary>
/// Collects structured telemetry from the thinking pipeline. Implementations must impose
/// minimal runtime cost — ideally non-blocking — since this sits on the hot path.
/// </summary>
public interface ITelemetryCollector
{
    Task RecordPipelineStartAsync(
        Guid requestId, Guid conversationId, DateTime startedAt,
        CancellationToken cancellationToken);

    Task RecordPipelineCompleteAsync(
        Guid requestId, PipelineOutcome outcome, ConfidenceScore? confidence,
        CancellationToken cancellationToken);

    Task RecordStageStartAsync(
        Guid requestId, string stageName, DateTime startedAt,
        CancellationToken cancellationToken);

    Task RecordStageCompleteAsync(
        Guid requestId, string stageName, StageOutcome outcome,
        int inputTokens, int outputTokens, string? errorMessage,
        CancellationToken cancellationToken);

    Task RecordDecisionAsync(
        Guid requestId, string stageName,
        string decisionType, string question, string chosen, string rationale,
        IReadOnlyList<DecisionCandidate>? candidates,
        CancellationToken cancellationToken);

    Task<PipelineTrace?> GetTraceAsync(Guid requestId, CancellationToken cancellationToken);

    Task<PagedTraces> GetRecentTracesAsync(
        int page, int pageSize, CancellationToken cancellationToken);
}

public sealed record PagedTraces(IReadOnlyList<PipelineTraceSummary> Items, int TotalCount);

public sealed record PipelineTraceSummary(
    Guid RequestId,
    Guid ConversationId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    PipelineOutcome Outcome,
    int TotalInputTokens,
    int TotalOutputTokens,
    double? ConfidenceOverall);
