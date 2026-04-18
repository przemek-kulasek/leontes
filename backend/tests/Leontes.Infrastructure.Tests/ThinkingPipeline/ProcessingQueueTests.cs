using Leontes.Application.Configuration;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.AI.ThinkingPipeline;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class ProcessingQueueTests
{
    private static ProcessingQueue CreateQueue(int capacity = 2, int enqueueWait = 1)
    {
        var opts = Options.Create(new ResilienceOptions
        {
            QueueCapacity = capacity,
            EnqueueWaitSeconds = enqueueWait
        });
        return new ProcessingQueue(opts, NullLogger<ProcessingQueue>.Instance);
    }

    private static ProcessingRequest Req(ProcessingRequestSource src) => new(
        CorrelationId: Guid.NewGuid(),
        ConversationId: Guid.NewGuid(),
        MessageId: Guid.NewGuid(),
        Content: "x",
        Channel: MessageChannel.Cli,
        Source: src,
        EnqueuedAt: DateTime.UtcNow);

    [Fact]
    public async Task EnqueueAsync_UserRequestWithRoom_ReturnsAccepted()
    {
        var queue = CreateQueue();

        var result = await queue.EnqueueAsync(Req(ProcessingRequestSource.User), TestContext.Current.CancellationToken);

        Assert.Equal(EnqueueResult.Accepted, result);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public async Task EnqueueAsync_SentinelAtCapacity_ReturnsDropped()
    {
        var queue = CreateQueue(capacity: 1);
        await queue.EnqueueAsync(Req(ProcessingRequestSource.User), TestContext.Current.CancellationToken);

        var result = await queue.EnqueueAsync(Req(ProcessingRequestSource.Sentinel), TestContext.Current.CancellationToken);

        Assert.Equal(EnqueueResult.Dropped, result);
    }

    [Fact]
    public async Task EnqueueAsync_UserAtCapacityWithShortWait_ReturnsBusy()
    {
        var queue = CreateQueue(capacity: 1, enqueueWait: 1);
        await queue.EnqueueAsync(Req(ProcessingRequestSource.User), TestContext.Current.CancellationToken);

        var result = await queue.EnqueueAsync(Req(ProcessingRequestSource.User), TestContext.Current.CancellationToken);

        Assert.Equal(EnqueueResult.Busy, result);
    }

    [Fact]
    public async Task DequeueAllAsync_DecrementsCountOnYield()
    {
        var queue = CreateQueue();
        await queue.EnqueueAsync(Req(ProcessingRequestSource.User), TestContext.Current.CancellationToken);
        await queue.EnqueueAsync(Req(ProcessingRequestSource.User), TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();
        var read = 0;
        try
        {
            await foreach (var _ in queue.DequeueAllAsync(cts.Token))
            {
                read++;
                if (read == 2)
                {
                    Assert.Equal(0, queue.Count);
                    await cts.CancelAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Equal(2, read);
    }
}
