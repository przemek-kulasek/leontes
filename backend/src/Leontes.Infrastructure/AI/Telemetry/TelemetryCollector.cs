using Leontes.Application;
using Leontes.Application.Configuration;
using Leontes.Application.Telemetry;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Producer side of telemetry capture. Writes are enqueued on a bounded channel and
/// flushed to the database by <see cref="TelemetryWriterService"/>. Hot-path methods
/// never await I/O — a channel stall drops the oldest pending event rather than block
/// the pipeline.
/// </summary>
internal sealed class TelemetryCollector(
    TelemetryChannel channel,
    SensitiveDataRedactor redactor,
    IServiceScopeFactory scopeFactory,
    IOptions<TelemetryOptions> options,
    ILogger<TelemetryCollector> logger) : ITelemetryCollector
{
    private readonly bool _enabled = options.Value.Enabled;

    public Task RecordPipelineStartAsync(
        Guid requestId, Guid conversationId, DateTime startedAt,
        CancellationToken cancellationToken)
    {
        if (_enabled)
            TryWrite(new PipelineStartedEvent(requestId, conversationId, startedAt));
        return Task.CompletedTask;
    }

    public Task RecordPipelineCompleteAsync(
        Guid requestId, PipelineOutcome outcome, ConfidenceScore? confidence,
        CancellationToken cancellationToken)
    {
        if (_enabled)
            TryWrite(new PipelineCompletedEvent(requestId, outcome, confidence, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public Task RecordStageStartAsync(
        Guid requestId, string stageName, DateTime startedAt,
        CancellationToken cancellationToken)
    {
        if (_enabled)
            TryWrite(new StageStartedEvent(requestId, stageName, startedAt));
        return Task.CompletedTask;
    }

    public Task RecordStageCompleteAsync(
        Guid requestId, string stageName, StageOutcome outcome,
        int inputTokens, int outputTokens, string? errorMessage,
        CancellationToken cancellationToken)
    {
        if (_enabled)
            TryWrite(new StageCompletedEvent(
                requestId, stageName, outcome, inputTokens, outputTokens,
                redactor.Redact(errorMessage), DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public Task RecordDecisionAsync(
        Guid requestId, string stageName,
        string decisionType, string question, string chosen, string rationale,
        IReadOnlyList<DecisionCandidate>? candidates,
        CancellationToken cancellationToken)
    {
        if (_enabled)
        {
            var redactedCandidates = candidates is null
                ? Array.Empty<DecisionCandidate>()
                : candidates
                    .Select(c => new DecisionCandidate(
                        redactor.Redact(c.Name),
                        c.Score,
                        redactor.Redact(c.Reason)))
                    .ToArray();

            TryWrite(new DecisionRecordedEvent(
                requestId, stageName, decisionType,
                redactor.Redact(question), redactor.Redact(chosen), redactor.Redact(rationale),
                redactedCandidates));
        }
        return Task.CompletedTask;
    }

    public async Task<PipelineTrace?> GetTraceAsync(Guid requestId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var trace = await db.PipelineTraces
            .AsNoTracking()
            .Where(t => t.RequestId == requestId)
            .Include(t => t.Stages)
                .ThenInclude(s => s.Decisions)
            .OrderByDescending(t => t.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return trace;
    }

    public async Task<PagedTraces> GetRecentTracesAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var skip = Math.Max(0, (page - 1) * pageSize);
        var totalCount = await db.PipelineTraces.CountAsync(cancellationToken);
        var items = await db.PipelineTraces
            .AsNoTracking()
            .OrderByDescending(t => t.StartedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(t => new PipelineTraceSummary(
                t.RequestId, t.ConversationId, t.StartedAt, t.CompletedAt,
                t.Outcome, t.TotalInputTokens, t.TotalOutputTokens, t.ConfidenceOverall))
            .ToListAsync(cancellationToken);

        return new PagedTraces(items, totalCount);
    }

    private void TryWrite(TelemetryEvent evt)
    {
        if (!channel.Channel.Writer.TryWrite(evt))
            logger.LogDebug("Telemetry channel full — dropping event for request {RequestId}", evt.RequestId);
    }
}
