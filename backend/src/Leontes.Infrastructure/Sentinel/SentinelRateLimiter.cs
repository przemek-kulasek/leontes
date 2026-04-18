using System.Collections.Concurrent;
using Leontes.Application.Sentinel;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Sentinel;

public sealed class SentinelRateLimiter(IOptions<SentinelOptions> options) : ISentinelRateLimiter
{
    private readonly ConcurrentDictionary<string, DateTime> _lastFired = new(StringComparer.OrdinalIgnoreCase);
    private readonly SentinelOptions _options = options.Value;

    public bool TryAcquire(string monitorSource, DateTime now)
    {
        var perMinute = Math.Max(1, _options.RateLimitPerMonitorPerMinute);
        var minInterval = TimeSpan.FromMinutes(1) / perMinute;

        while (true)
        {
            if (_lastFired.TryGetValue(monitorSource, out var last))
            {
                if (now - last < minInterval)
                    return false;

                if (_lastFired.TryUpdate(monitorSource, now, last))
                    return true;
            }
            else if (_lastFired.TryAdd(monitorSource, now))
            {
                return true;
            }
        }
    }
}
