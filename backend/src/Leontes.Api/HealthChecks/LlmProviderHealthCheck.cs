using Leontes.Application.ThinkingPipeline;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Leontes.Api.HealthChecks;

public sealed class LlmProviderHealthCheck(ILlmAvailability availability) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (availability.IsAvailable)
        {
            return Task.FromResult(HealthCheckResult.Healthy("LLM provider responding"));
        }

        var description =
            $"LLM provider degraded ({availability.ConsecutiveFailures} consecutive failures, last at {availability.LastFailureAt:O})";
        return Task.FromResult(HealthCheckResult.Degraded(description));
    }
}
