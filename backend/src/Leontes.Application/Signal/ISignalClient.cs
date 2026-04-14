namespace Leontes.Application.Signal;

public interface ISignalClient
{
    Task<IReadOnlyList<SignalIncomingMessage>> ReceiveMessagesAsync(CancellationToken cancellationToken);
    Task SendMessageAsync(string recipient, string message, CancellationToken cancellationToken);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}
