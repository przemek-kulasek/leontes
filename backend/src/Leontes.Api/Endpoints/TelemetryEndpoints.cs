using Leontes.Application;
using Leontes.Application.Telemetry;
using Leontes.Domain.Entities;
using Leontes.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Leontes.Api.Endpoints;

public static class TelemetryEndpoints
{
    public static RouteGroupBuilder MapTelemetryEndpoints(this RouteGroupBuilder group)
    {
        var telemetry = group.MapGroup("/telemetry").WithTags("Telemetry");

        telemetry.MapGet("/traces/{requestId:guid}", GetTrace)
            .WithName("GetPipelineTrace")
            .WithSummary("Get the full pipeline trace (stages + decisions) for a request")
            .Produces<PipelineTrace>()
            .Produces(404);

        telemetry.MapGet("/traces", ListTraces)
            .WithName("ListPipelineTraces")
            .WithSummary("List recent pipeline traces, newest first")
            .Produces<PagedResponse<PipelineTraceSummary>>();

        telemetry.MapGet("/decisions/{requestId:guid}", GetDecisions)
            .WithName("GetDecisionsForRequest")
            .WithSummary("Get all decision records emitted for a request")
            .Produces<IReadOnlyList<DecisionRecord>>()
            .Produces(404);

        telemetry.MapGet("/metrics/summary", GetCurrentMetrics)
            .WithName("GetCurrentMetricsSummary")
            .WithSummary("Get the most recent aggregated metrics period")
            .Produces<MetricsSummary>()
            .Produces(404);

        telemetry.MapGet("/metrics/history", GetMetricsHistory)
            .WithName("GetMetricsHistory")
            .WithSummary("List historical metrics summaries")
            .Produces<PagedResponse<MetricsSummary>>();

        telemetry.MapGet("/explain/{requestId:guid}", Explain)
            .WithName("ExplainRequest")
            .WithSummary("Natural-language explanation of a prior request's trace")
            .Produces<ExplanationResponse>()
            .Produces(404);

        return group;
    }

    private static async Task<IResult> GetTrace(
        Guid requestId,
        ITelemetryCollector collector,
        CancellationToken cancellationToken)
    {
        var trace = await collector.GetTraceAsync(requestId, cancellationToken);
        return trace is null
            ? Results.NotFound()
            : Results.Ok(trace);
    }

    private static async Task<IResult> ListTraces(
        ITelemetryCollector collector,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(page, pageSize);

        var paged = await collector.GetRecentTracesAsync(page, pageSize, cancellationToken);
        return Results.Ok(new PagedResponse<PipelineTraceSummary>(paged.Items, page, pageSize, paged.TotalCount));
    }

    private static async Task<IResult> GetDecisions(
        Guid requestId,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var trace = await db.PipelineTraces
            .AsNoTracking()
            .Where(t => t.RequestId == requestId)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (trace == Guid.Empty)
            return Results.NotFound();

        var decisions = await db.DecisionRecords
            .AsNoTracking()
            .Where(d => d.StageTrace!.PipelineTraceId == trace)
            .ToListAsync(cancellationToken);

        return Results.Ok(decisions);
    }

    private static async Task<IResult> GetCurrentMetrics(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var latest = await db.MetricsSummaries
            .AsNoTracking()
            .OrderByDescending(m => m.PeriodStart)
            .FirstOrDefaultAsync(cancellationToken);

        return latest is null ? Results.NotFound() : Results.Ok(latest);
    }

    private static async Task<IResult> GetMetricsHistory(
        IApplicationDbContext db,
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(page, pageSize);

        var total = await db.MetricsSummaries.CountAsync(cancellationToken);
        var items = await db.MetricsSummaries
            .AsNoTracking()
            .OrderByDescending(m => m.PeriodStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<MetricsSummary>(items, page, pageSize, total));
    }

    private static async Task<IResult> Explain(
        Guid requestId,
        IExplainabilityService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var explanation = await service.ExplainAsync(requestId, cancellationToken);
            return Results.Ok(new ExplanationResponse(requestId, explanation));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page <= 0)
            throw new ValidationException("'page' must be greater than 0.");

        if (pageSize <= 0 || pageSize > 100)
            throw new ValidationException("'pageSize' must be between 1 and 100.");
    }
}

public sealed record ExplanationResponse(Guid RequestId, string Explanation);
