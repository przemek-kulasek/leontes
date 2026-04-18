using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Internal channel messages between <see cref="TelemetryCollector"/> (producer) and
/// <see cref="TelemetryWriterService"/> (consumer).
/// </summary>
internal abstract record TelemetryEvent(Guid RequestId);

internal sealed record PipelineStartedEvent(
    Guid RequestId, Guid ConversationId, DateTime StartedAt) : TelemetryEvent(RequestId);

internal sealed record PipelineCompletedEvent(
    Guid RequestId, PipelineOutcome Outcome, ConfidenceScore? Confidence, DateTime CompletedAt)
    : TelemetryEvent(RequestId);

internal sealed record StageStartedEvent(
    Guid RequestId, string StageName, DateTime StartedAt) : TelemetryEvent(RequestId);

internal sealed record StageCompletedEvent(
    Guid RequestId, string StageName, StageOutcome Outcome,
    int InputTokens, int OutputTokens, string? ErrorMessage, DateTime CompletedAt)
    : TelemetryEvent(RequestId);

internal sealed record DecisionRecordedEvent(
    Guid RequestId, string StageName, string DecisionType, string Question,
    string Chosen, string Rationale, IReadOnlyList<DecisionCandidate> Candidates)
    : TelemetryEvent(RequestId);
