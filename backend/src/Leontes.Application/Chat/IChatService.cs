namespace Leontes.Application.Chat;

public interface IChatService
{
    Task<Guid> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamResponseAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid conversationId, int limit, CancellationToken cancellationToken = default);
}
