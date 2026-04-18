using System.Threading.Channels;
using Leontes.Application.CostControl;

namespace Leontes.Infrastructure.AI.CostControl;

internal sealed class TokenUsageChannel
{
    public Channel<TokenUsage> Channel { get; } =
        System.Threading.Channels.Channel.CreateBounded<TokenUsage>(
            new BoundedChannelOptions(capacity: 4096)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
}
