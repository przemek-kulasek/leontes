using System.Threading.Channels;
using Leontes.Application.Configuration;
using Leontes.Application.ThinkingPipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

public sealed class ProcessingQueue : IProcessingQueue
{
    private readonly Channel<ProcessingRequest> _channel;
    private readonly TimeSpan _enqueueWait;
    private readonly ILogger<ProcessingQueue> _logger;
    private int _count;

    public ProcessingQueue(
        IOptions<ResilienceOptions> options,
        ILogger<ProcessingQueue> logger)
    {
        var opts = options.Value;
        Capacity = Math.Max(1, opts.QueueCapacity);
        _enqueueWait = TimeSpan.FromSeconds(Math.Max(1, opts.EnqueueWaitSeconds));
        _logger = logger;

        _channel = Channel.CreateBounded<ProcessingRequest>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    }

    public int Count => Volatile.Read(ref _count);
    public int Capacity { get; }

    public async ValueTask<EnqueueResult> EnqueueAsync(
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Source == ProcessingRequestSource.Sentinel)
        {
            if (_channel.Writer.TryWrite(request))
            {
                Interlocked.Increment(ref _count);
                return EnqueueResult.Accepted;
            }

            _logger.LogWarning(
                "Processing queue full ({Count}/{Capacity}) — dropping Sentinel event {CorrelationId}",
                Count, Capacity, request.CorrelationId);
            return EnqueueResult.Dropped;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_enqueueWait);

        try
        {
            await _channel.Writer.WriteAsync(request, cts.Token);
            Interlocked.Increment(ref _count);
            return EnqueueResult.Accepted;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Processing queue busy — rejecting user request {CorrelationId} after {WaitSeconds}s",
                request.CorrelationId, _enqueueWait.TotalSeconds);
            return EnqueueResult.Busy;
        }
    }

    public async IAsyncEnumerable<ProcessingRequest> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _count);
            yield return request;
        }
    }
}
