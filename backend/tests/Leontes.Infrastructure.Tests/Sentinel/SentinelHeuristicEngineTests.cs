using Leontes.Application.Sentinel;
using Leontes.Infrastructure.Sentinel;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leontes.Infrastructure.Tests.Sentinel;

public sealed class SentinelHeuristicEngineTests
{
    [Fact]
    public void Process_UnknownMonitor_ReturnsNull()
    {
        var engine = new SentinelHeuristicEngine([new ClipboardContentFilter()], NullLogger<SentinelHeuristicEngine>.Instance);

        var result = engine.Process("Unknown", "payload", new Dictionary<string, string>());

        Assert.Null(result);
    }

    [Fact]
    public void Process_FilterThrows_LogsAndReturnsNull()
    {
        var engine = new SentinelHeuristicEngine([new ThrowingFilter()], NullLogger<SentinelHeuristicEngine>.Instance);

        var result = engine.Process("Throwing", "payload", new Dictionary<string, string>());

        Assert.Null(result);
    }

    [Fact]
    public void Process_DelegatesToMatchingFilter()
    {
        var engine = new SentinelHeuristicEngine([new ClipboardContentFilter()], NullLogger<SentinelHeuristicEngine>.Instance);

        var result = engine.Process("Clipboard", "alice@example.com", new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal("email", result!.Pattern);
    }

    private sealed class ThrowingFilter : ISentinelFilter
    {
        public string MonitorSource => "Throwing";

        public SentinelEvent? Evaluate(string rawEvent, IReadOnlyDictionary<string, string> metadata) =>
            throw new InvalidOperationException("boom");
    }
}
