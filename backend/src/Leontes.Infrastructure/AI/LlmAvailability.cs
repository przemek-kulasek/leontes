using Leontes.Application.Configuration;
using Leontes.Application.ThinkingPipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI;

public sealed class LlmAvailability(
    IOptions<ResilienceOptions> options,
    ILogger<LlmAvailability> logger) : ILlmAvailability
{
    private readonly LlmResilienceOptions _options = options.Value.Llm;
    private readonly Lock _lock = new();
    private int _consecutiveFailures;
    private DateTime? _lastFailureAt;
    private DateTime? _firstFailureInWindowAt;
    private bool _isAvailable = true;

    public bool IsAvailable
    {
        get { lock (_lock) return _isAvailable; }
    }

    public DateTime? LastFailureAt
    {
        get { lock (_lock) return _lastFailureAt; }
    }

    public int ConsecutiveFailures
    {
        get { lock (_lock) return _consecutiveFailures; }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            var wasDegraded = !_isAvailable;
            _consecutiveFailures = 0;
            _firstFailureInWindowAt = null;
            _isAvailable = true;
            if (wasDegraded)
            {
                logger.LogInformation("LLM provider recovered");
            }
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            _lastFailureAt = now;

            var window = TimeSpan.FromMinutes(Math.Max(1, _options.DegradedWindowMinutes));
            if (_firstFailureInWindowAt is null || now - _firstFailureInWindowAt > window)
            {
                _firstFailureInWindowAt = now;
                _consecutiveFailures = 1;
            }
            else
            {
                _consecutiveFailures++;
            }

            if (_isAvailable &&
                _consecutiveFailures >= Math.Max(1, _options.ConsecutiveFailuresBeforeDegraded))
            {
                _isAvailable = false;
                logger.LogError(
                    "LLM provider entered degraded mode after {Failures} consecutive failures in {Window}",
                    _consecutiveFailures, window);
            }
        }
    }
}
