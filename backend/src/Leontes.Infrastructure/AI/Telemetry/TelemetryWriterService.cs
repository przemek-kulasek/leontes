using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Drains the <see cref="TelemetryChannel"/> and persists events to PostgreSQL.
/// Correlates events by RequestId, resolving stage and pipeline rows in-place.
/// Failures are logged and swallowed — telemetry loss must never crash the host.
/// </summary>
internal sealed class TelemetryWriterService(
    TelemetryChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<TelemetryWriterService> logger) : BackgroundService
{
    private const int BatchSize = 64;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = channel.Channel.Reader;

        while (await reader.WaitToReadAsync(stoppingToken))
        {
            var batch = new List<TelemetryEvent>(BatchSize);

            while (batch.Count < BatchSize && reader.TryRead(out var evt))
                batch.Add(evt);

            try
            {
                await PersistAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist telemetry batch of size {Count}", batch.Count);
            }
        }
    }

    private async Task PersistAsync(IReadOnlyList<TelemetryEvent> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var requestIds = batch.Select(e => e.RequestId).Distinct().ToArray();

        var existingTraces = await db.PipelineTraceSet
            .Where(t => requestIds.Contains(t.RequestId))
            .Include(t => t.Stages)
            .ToDictionaryAsync(t => t.RequestId, cancellationToken);

        foreach (var evt in batch)
            Apply(db, existingTraces, evt);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(
        ApplicationDbContext db,
        Dictionary<Guid, PipelineTrace> traces,
        TelemetryEvent evt)
    {
        switch (evt)
        {
            case PipelineStartedEvent started:
                if (!traces.ContainsKey(started.RequestId))
                {
                    var trace = new PipelineTrace
                    {
                        Id = Guid.NewGuid(),
                        RequestId = started.RequestId,
                        ConversationId = started.ConversationId,
                        StartedAt = started.StartedAt,
                        Outcome = PipelineOutcome.Success
                    };
                    db.PipelineTraceSet.Add(trace);
                    traces[started.RequestId] = trace;
                }
                break;

            case PipelineCompletedEvent completed when traces.TryGetValue(completed.RequestId, out var pipeline):
                pipeline.CompletedAt = completed.CompletedAt;
                pipeline.Outcome = completed.Outcome;
                pipeline.TotalInputTokens = pipeline.Stages.Sum(s => s.InputTokens);
                pipeline.TotalOutputTokens = pipeline.Stages.Sum(s => s.OutputTokens);
                if (completed.Confidence is { } c)
                {
                    pipeline.ConfidenceOverall = c.Overall;
                    pipeline.ConfidenceMemorySupport = c.Breakdown.MemorySupport;
                    pipeline.ConfidenceGraphSupport = c.Breakdown.GraphSupport;
                    pipeline.ConfidenceConversationClarity = c.Breakdown.ConversationClarity;
                    pipeline.ConfidenceToolReliability = c.Breakdown.ToolReliability;
                }
                break;

            case StageStartedEvent stageStarted when traces.TryGetValue(stageStarted.RequestId, out var parent):
                if (!parent.Stages.Any(s => s.StageName == stageStarted.StageName && s.CompletedAt is null))
                {
                    var stage = new StageTrace
                    {
                        Id = Guid.NewGuid(),
                        PipelineTraceId = parent.Id,
                        StageName = stageStarted.StageName,
                        StartedAt = stageStarted.StartedAt,
                        Outcome = StageOutcome.Success,
                        PipelineTrace = parent
                    };
                    // Explicit Add forces Added state. Adding only to the navigation collection
                    // makes EF infer state from the (non-default) Guid key, which mis-classifies
                    // the new stage as Modified and triggers a concurrency-failed UPDATE.
                    db.StageTraceSet.Add(stage);
                    parent.Stages.Add(stage);
                }
                break;

            case StageCompletedEvent stageCompleted when traces.TryGetValue(stageCompleted.RequestId, out var parent):
                var activeStage = parent.Stages.LastOrDefault(s =>
                    s.StageName == stageCompleted.StageName && s.CompletedAt is null);
                if (activeStage is not null)
                {
                    activeStage.CompletedAt = stageCompleted.CompletedAt;
                    activeStage.Outcome = stageCompleted.Outcome;
                    activeStage.InputTokens = stageCompleted.InputTokens;
                    activeStage.OutputTokens = stageCompleted.OutputTokens;
                    activeStage.ErrorMessage = stageCompleted.ErrorMessage;
                }
                break;

            case DecisionRecordedEvent decision when traces.TryGetValue(decision.RequestId, out var parent):
                var decisionStage = parent.Stages.LastOrDefault(s => s.StageName == decision.StageName)
                    ?? parent.Stages.LastOrDefault();
                if (decisionStage is not null)
                {
                    var decisionRecord = new DecisionRecord
                    {
                        Id = Guid.NewGuid(),
                        StageTraceId = decisionStage.Id,
                        DecisionType = decision.DecisionType,
                        Question = decision.Question,
                        Chosen = decision.Chosen,
                        Rationale = decision.Rationale,
                        Candidates = decision.Candidates,
                        StageTrace = decisionStage
                    };
                    db.DecisionRecordSet.Add(decisionRecord);
                    decisionStage.Decisions.Add(decisionRecord);
                }
                break;
        }
    }
}
