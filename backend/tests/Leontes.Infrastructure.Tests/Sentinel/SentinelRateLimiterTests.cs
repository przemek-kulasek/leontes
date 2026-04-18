using Leontes.Application.Sentinel;
using Leontes.Infrastructure.Sentinel;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.Sentinel;

public sealed class SentinelRateLimiterTests
{
    [Fact]
    public void TryAcquire_FirstEvent_Allowed()
    {
        var limiter = CreateLimiter(perMinute: 1);
        var now = DateTime.UtcNow;

        Assert.True(limiter.TryAcquire("Clipboard", now));
    }

    [Fact]
    public void TryAcquire_SecondEventWithinWindow_Rejected()
    {
        var limiter = CreateLimiter(perMinute: 1);
        var now = DateTime.UtcNow;

        limiter.TryAcquire("Clipboard", now);

        Assert.False(limiter.TryAcquire("Clipboard", now.AddSeconds(30)));
    }

    [Fact]
    public void TryAcquire_AfterWindow_Allowed()
    {
        var limiter = CreateLimiter(perMinute: 1);
        var now = DateTime.UtcNow;

        limiter.TryAcquire("Clipboard", now);

        Assert.True(limiter.TryAcquire("Clipboard", now.AddMinutes(2)));
    }

    [Fact]
    public void TryAcquire_DifferentMonitors_Independent()
    {
        var limiter = CreateLimiter(perMinute: 1);
        var now = DateTime.UtcNow;

        Assert.True(limiter.TryAcquire("Clipboard", now));
        Assert.True(limiter.TryAcquire("FileSystem", now));
    }

    [Fact]
    public void TryAcquire_HigherRate_AllowsMoreEvents()
    {
        var limiter = CreateLimiter(perMinute: 6);
        var now = DateTime.UtcNow;

        Assert.True(limiter.TryAcquire("Clipboard", now));
        Assert.True(limiter.TryAcquire("Clipboard", now.AddSeconds(11)));
    }

    private static SentinelRateLimiter CreateLimiter(int perMinute) =>
        new(Options.Create(new SentinelOptions { RateLimitPerMonitorPerMinute = perMinute }));
}
