using Leontes.Domain.Enums;

namespace Leontes.Application.Messaging;

public interface IMessagingClient
{
    MessageChannel Channel { get; }
    Task<IReadOnlyList<IncomingMessage>> ReceiveMessagesAsync(CancellationToken cancellationToken);
    Task SendMessageAsync(string recipient, string message, CancellationToken cancellationToken);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}
