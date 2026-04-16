using System.Collections.Concurrent;
using Leontes.Application.ProactiveCommunication;
using Microsoft.Agents.AI.Workflows;

namespace Leontes.Infrastructure.ProactiveCommunication;

public sealed class WorkflowSessionManager : IWorkflowSessionManager
{
    private StreamingRun? _activeRun;
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, ExternalRequest> _pendingRequests = new();

    public void SetActiveRun(StreamingRun run)
    {
        lock (_lock)
        {
            _activeRun = run;
        }
    }

    public StreamingRun? GetActiveRun()
    {
        lock (_lock)
        {
            return _activeRun;
        }
    }

    public void ClearActiveRun()
    {
        lock (_lock)
        {
            _activeRun = null;
        }

        _pendingRequests.Clear();
    }

    public void TrackPendingRequest(ExternalRequest request)
    {
        _pendingRequests[request.RequestId] = request;
    }

    public ExternalRequest? TakePendingRequest(string requestId)
    {
        return _pendingRequests.TryRemove(requestId, out var request) ? request : null;
    }
}
