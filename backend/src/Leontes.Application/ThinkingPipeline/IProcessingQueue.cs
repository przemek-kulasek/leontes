namespace Leontes.Application.ThinkingPipeline;

public interface IProcessingQueue
{
    int Count { get; }
    int Capacity { get; }

    ValueTask<EnqueueResult> EnqueueAsync(ProcessingRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<ProcessingRequest> DequeueAllAsync(CancellationToken cancellationToken);
}

public enum EnqueueResult
{
    Accepted = 0,
    Dropped = 1,
    Busy = 2
}
