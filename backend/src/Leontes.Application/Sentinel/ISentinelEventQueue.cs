namespace Leontes.Application.Sentinel;

public interface ISentinelEventQueue
{
    ValueTask<bool> TryEnqueueAsync(SentinelEvent evt, CancellationToken cancellationToken);

    IAsyncEnumerable<SentinelEvent> ReadAllAsync(CancellationToken cancellationToken);
}
