using Leontes.Application.Configuration;
using Leontes.Application.ProactiveCommunication;
using Leontes.Infrastructure.ProactiveCommunication;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.ProactiveCommunication;

/// <summary>
/// ExternalRequest can only be produced by a live workflow run, so the full
/// fire path is exercised by integration tests. These unit tests guard the
/// dispose/cancel behavior.
/// </summary>
public sealed class RequestPortTimeoutSchedulerTests
{
    private static RequestPortTimeoutScheduler Build()
    {
        var options = Options.Create(new ResilienceOptions());
        return new RequestPortTimeoutScheduler(
            new FakeSessions(),
            new FakeBridge(),
            options,
            NullLogger<RequestPortTimeoutScheduler>.Instance);
    }

    [Fact]
    public void Cancel_UnknownRequestId_DoesNotThrow()
    {
        var scheduler = Build();

        scheduler.Cancel("nonexistent-request");
    }

    [Fact]
    public void Dispose_WithNoPendingRequests_DoesNotThrow()
    {
        var scheduler = Build();

        scheduler.Dispose();
    }

    private sealed class FakeSessions : IWorkflowSessionManager
    {
        public void SetActiveRun(StreamingRun run) { }
        public StreamingRun? GetActiveRun() => null;
        public void ClearActiveRun() { }
        public void TrackPendingRequest(ExternalRequest request) { }
        public ExternalRequest? TakePendingRequest(string requestId) => null;
    }

    private sealed class FakeBridge : IWorkflowEventBridge
    {
        public void RegisterClient(string clientId) { }
        public void UnregisterClient(string clientId) { }
        public bool HasActiveClients => false;
        public Task PublishEventAsync(WorkflowEvent evt, CancellationToken cancellationToken) => Task.CompletedTask;
        public IAsyncEnumerable<WorkflowEvent> ReadEventsAsync(string clientId, CancellationToken cancellationToken)
            => Empty();

        private static async IAsyncEnumerable<WorkflowEvent> Empty()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
