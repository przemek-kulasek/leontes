using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Leontes.Integration.Tests.Infrastructure;

internal sealed class TestChatClient : IChatClient
{
    private const string TestResponse = "The current date and time is Thursday, April 10, 2026 12:00 PM.";

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatResponse(
            [new ChatMessage(ChatRole.Assistant, TestResponse)]));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);

        foreach (var message in response.Messages)
        {
            foreach (var content in message.Contents)
            {
                yield return new ChatResponseUpdate(message.Role, [content]);
            }
        }
    }
}
