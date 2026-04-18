using Leontes.Application.ThinkingPipeline;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Leontes.Api.HealthChecks;

public sealed class ProcessingQueueHealthCheck(IProcessingQueue queue) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var count = queue.Count;
        var capacity = Math.Max(1, queue.Capacity);
        var ratio = (double)count / capacity;
        var data = new Dictionary<string, object> { ["count"] = count, ["capacity"] = capacity };

        if (ratio < 0.5)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"Queue at {count}/{capacity}", data));
        }
        if (ratio < 0.9)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"Queue at {count}/{capacity}", data: data));
        }
        return Task.FromResult(HealthCheckResult.Unhealthy($"Queue near capacity {count}/{capacity}", data: data));
    }
}
