namespace Leontes.Application.Sentinel;

public interface ISentinelRateLimiter
{
    bool TryAcquire(string monitorSource, DateTime now);
}
