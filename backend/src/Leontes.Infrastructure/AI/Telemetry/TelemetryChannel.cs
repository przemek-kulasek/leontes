using System.Threading.Channels;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Singleton bounded channel shared between <see cref="TelemetryCollector"/> (producer)
/// and <see cref="TelemetryWriterService"/> (consumer). The bounded capacity plus
/// <see cref="BoundedChannelFullMode.DropOldest"/> guarantees the pipeline hot path
/// is never blocked by a slow writer or database stall.
/// </summary>
internal sealed class TelemetryChannel
{
    public Channel<TelemetryEvent> Channel { get; } =
        System.Threading.Channels.Channel.CreateBounded<TelemetryEvent>(
            new BoundedChannelOptions(capacity: 4096)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
}
