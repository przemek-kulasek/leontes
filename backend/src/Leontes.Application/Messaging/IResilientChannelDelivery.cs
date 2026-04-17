using Leontes.Domain.Enums;

namespace Leontes.Application.Messaging;

public interface IResilientChannelDelivery
{
    Task<DeliveryResult> DeliverAsync(
        OutboundMessage message,
        CancellationToken cancellationToken);
}

public sealed record OutboundMessage(
    string Recipient,
    string Content,
    MessageChannel PreferredChannel,
    string? RequestId = null);

public sealed record DeliveryResult(
    bool Delivered,
    MessageChannel Channel,
    MessageChannel? FallbackUsed,
    string? ErrorReason);
