using System.Text;
using Leontes.Application;
using Leontes.Application.Telemetry;
using Leontes.Domain.Entities;
using Leontes.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Formats a stored pipeline trace into a short natural-language explanation.
/// Never calls the LLM — the answer is a readout of what actually happened.
/// </summary>
internal sealed class ExplainabilityService(IApplicationDbContext db) : IExplainabilityService
{
    public async Task<string> ExplainAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var trace = await db.PipelineTraces
            .AsNoTracking()
            .Where(t => t.RequestId == requestId)
            .Include(t => t.Stages.OrderBy(s => s.StartedAt))
                .ThenInclude(s => s.Decisions)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"No trace found for request {requestId}.");

        return Render(trace);
    }

    private static string Render(PipelineTrace trace)
    {
        var sb = new StringBuilder();

        var elapsed = (trace.CompletedAt ?? DateTime.UtcNow) - trace.StartedAt;
        sb.Append("For request ").Append(trace.RequestId).Append(", ");
        sb.Append("I ran ").Append(trace.Stages.Count).Append(" pipeline stages in ");
        sb.Append(elapsed.TotalMilliseconds.ToString("F0")).Append(" ms ");
        sb.Append("(outcome: ").Append(trace.Outcome).Append("). ");

        foreach (var stage in trace.Stages.OrderBy(s => s.StartedAt))
        {
            sb.Append("In ").Append(stage.StageName).Append(" I ");
            sb.Append(stage.Outcome switch
            {
                Domain.Enums.StageOutcome.Skipped => "skipped work",
                Domain.Enums.StageOutcome.Degraded => "ran in degraded mode",
                Domain.Enums.StageOutcome.Failed => "failed",
                _ => "succeeded"
            });

            if (stage.Decisions.Count > 0)
            {
                sb.Append(" — ");
                sb.Append(string.Join("; ", stage.Decisions.Select(d =>
                    $"{d.DecisionType}: chose '{d.Chosen}' ({d.Rationale})")));
            }

            sb.Append(". ");
        }

        if (trace.ConfidenceOverall is { } conf)
            sb.Append("My confidence was ").Append(conf.ToString("F2")).Append('.');

        return sb.ToString();
    }
}
