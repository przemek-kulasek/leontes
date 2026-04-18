using System.Runtime.CompilerServices;
using Leontes.Application.Configuration;
using Leontes.Application.ThinkingPipeline;
using Leontes.Infrastructure.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.AI;

public sealed class ResilientChatClientTests
{
    private static (ResilientChatClient Client, FakeChatClient Inner, FakeAvailability Availability) Build(
        int maxRetries = 2,
        int timeoutSeconds = 30,
        Func<int, Task<ChatResponse>>? behavior = null)
    {
        var inner = new FakeChatClient(behavior);
        var availability = new FakeAvailability();
        var options = Options.Create(new ResilienceOptions
        {
            Llm = new LlmResilienceOptions
            {
                MaxRetries = maxRetries,
                TimeoutSeconds = timeoutSeconds,
                RetryBaseDelaySeconds = 1
            }
        });
        var client = new ResilientChatClient(inner, availability, options, NullLogger<ResilientChatClient>.Instance);
        return (client, inner, availability);
    }

    [Fact]
    public async Task GetResponseAsync_Success_RecordsSuccess()
    {
        var (client, inner, availability) = Build();

        var response = await client.GetResponseAsync(
            [new(ChatRole.User, "hi")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal(1, inner.Calls);
        Assert.True(availability.SuccessCalled);
    }

    [Fact]
    public async Task GetResponseAsync_TransientFailure_RetriesAndSucceeds()
    {
        var (client, inner, availability) = Build(
            maxRetries: 2,
            behavior: attempt => attempt switch
            {
                1 => throw new HttpRequestException("503 Service Unavailable"),
                _ => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]))
            });

        var response = await client.GetResponseAsync(
            [new(ChatRole.User, "hi")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.Calls);
        Assert.True(availability.SuccessCalled);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetResponseAsync_AuthFailure_DoesNotRetry()
    {
        var (client, inner, availability) = Build(
            maxRetries: 3,
            behavior: _ => throw new InvalidOperationException("401 Unauthorized"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.GetResponseAsync(
                [new(ChatRole.User, "hi")],
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(1, inner.Calls);
        Assert.True(availability.FailureCalled);
    }

    [Fact]
    public async Task GetResponseAsync_ExhaustedRetries_RecordsFailure()
    {
        var (client, inner, availability) = Build(
            maxRetries: 1,
            behavior: _ => throw new HttpRequestException("500"));

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.GetResponseAsync(
                [new(ChatRole.User, "hi")],
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(2, inner.Calls);
        Assert.True(availability.FailureCalled);
    }

    private sealed class FakeAvailability : ILlmAvailability
    {
        public bool IsAvailable { get; private set; } = true;
        public DateTime? LastFailureAt { get; private set; }
        public int ConsecutiveFailures { get; private set; }
        public bool SuccessCalled { get; private set; }
        public bool FailureCalled { get; private set; }

        public void RecordSuccess() { SuccessCalled = true; IsAvailable = true; ConsecutiveFailures = 0; }
        public void RecordFailure()
        {
            FailureCalled = true;
            ConsecutiveFailures++;
            LastFailureAt = DateTime.UtcNow;
        }
    }

    private sealed class FakeChatClient(Func<int, Task<ChatResponse>>? behavior) : IChatClient
    {
        public int Calls;
        private readonly Func<int, Task<ChatResponse>> _behavior =
            behavior ?? (_ => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])));

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return _behavior(Calls);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Empty(cancellationToken);

        private static async IAsyncEnumerable<ChatResponseUpdate> Empty(
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
