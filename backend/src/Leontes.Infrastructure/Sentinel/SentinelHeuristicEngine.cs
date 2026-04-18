using Leontes.Application.Sentinel;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.Sentinel;

public sealed class SentinelHeuristicEngine(
    IEnumerable<ISentinelFilter> filters,
    ILogger<SentinelHeuristicEngine> logger) : ISentinelHeuristicEngine
{
    private readonly Dictionary<string, ISentinelFilter> _filters = filters.ToDictionary(
        f => f.MonitorSource,
        StringComparer.OrdinalIgnoreCase);

    public SentinelEvent? Process(
        string monitorSource,
        string rawEvent,
        IReadOnlyDictionary<string, string> metadata)
    {
        if (!_filters.TryGetValue(monitorSource, out var filter))
        {
            logger.LogDebug("No Sentinel filter registered for {MonitorSource}", monitorSource);
            return null;
        }

        try
        {
            return filter.Evaluate(rawEvent, metadata);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sentinel filter for {MonitorSource} threw; discarding event", monitorSource);
            return null;
        }
    }
}
