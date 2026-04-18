using Leontes.Application.Sentinel;
using Leontes.Infrastructure.Sentinel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.Sentinel;

public sealed class SentinelEventQueueTests
{
    [Fact]
    public async Task TryEnqueueAsync_BelowCapacity_ReturnsTrue()
    {
        var queue = CreateQueue(capacity: 2);

        var accepted = await queue.TryEnqueueAsync(Event("a"), TestContext.Current.CancellationToken);

        Assert.True(accepted);
    }

    [Fact]
    public async Task TryEnqueueAsync_AtCapacity_DropsAndReturnsFalse()
    {
        var queue = CreateQueue(capacity: 2);

        await queue.TryEnqueueAsync(Event("a"), TestContext.Current.CancellationToken);
        await queue.TryEnqueueAsync(Event("b"), TestContext.Current.CancellationToken);
        var overflow = await queue.TryEnqueueAsync(Event("c"), TestContext.Current.CancellationToken);

        Assert.False(overflow);
    }

    [Fact]
    public async Task ReadAllAsync_YieldsEnqueuedEventsInOrder()
    {
        var queue = CreateQueue(capacity: 4);
        await queue.TryEnqueueAsync(Event("a"), TestContext.Current.CancellationToken);
        await queue.TryEnqueueAsync(Event("b"), TestContext.Current.CancellationToken);

        var patterns = new List<string?>();
        await using var enumerator = queue
            .ReadAllAsync(TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);
        for (var i = 0; i < 2; i++)
        {
            Assert.True(await enumerator.MoveNextAsync());
            patterns.Add(enumerator.Current.Pattern);
        }

        Assert.Equal(["a", "b"], patterns);
    }

    private static SentinelEventQueue CreateQueue(int capacity) =>
        new(Options.Create(new SentinelOptions { QueueCapacity = capacity }),
            NullLogger<SentinelEventQueue>.Instance);

    private static SentinelEvent Event(string pattern) => new(
        MonitorSource: "Test",
        EventType: "test",
        Pattern: pattern,
        Summary: "test",
        Metadata: new Dictionary<string, string>(),
        OccurredAt: DateTime.UtcNow,
        Priority: SentinelPriority.Low);
}
