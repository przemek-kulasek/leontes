using System.Threading.Channels;
using Leontes.Application.Sentinel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Sentinel;

public sealed class SentinelEventQueue : ISentinelEventQueue
{
    private readonly Channel<SentinelEvent> _channel;
    private readonly ILogger<SentinelEventQueue> _logger;

    public SentinelEventQueue(IOptions<SentinelOptions> options, ILogger<SentinelEventQueue> logger)
    {
        var capacity = Math.Max(1, options.Value.QueueCapacity);
        _channel = Channel.CreateBounded<SentinelEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _logger = logger;
    }

    public ValueTask<bool> TryEnqueueAsync(SentinelEvent evt, CancellationToken cancellationToken)
    {
        if (_channel.Writer.TryWrite(evt))
            return ValueTask.FromResult(true);

        _logger.LogWarning(
            "Sentinel event queue full — dropped event {EventType}/{Pattern} from {MonitorSource}",
            evt.EventType, evt.Pattern, evt.MonitorSource);
        return ValueTask.FromResult(false);
    }

    public IAsyncEnumerable<SentinelEvent> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
