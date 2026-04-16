using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication;

public interface IWorkflowSessionManager
{
    void SetActiveRun(StreamingRun run);
    StreamingRun? GetActiveRun();
    void ClearActiveRun();

    void TrackPendingRequest(ExternalRequest request);
    ExternalRequest? TakePendingRequest(string requestId);
}
