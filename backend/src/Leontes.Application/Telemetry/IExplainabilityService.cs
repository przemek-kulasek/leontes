namespace Leontes.Application.Telemetry;

/// <summary>
/// Produces a natural-language explanation of a prior request's pipeline trace —
/// the "why did you do that?" feature. Built from stored trace data, no LLM call.
/// </summary>
public interface IExplainabilityService
{
    Task<string> ExplainAsync(Guid requestId, CancellationToken cancellationToken);
}
