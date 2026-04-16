using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication;

public interface IWorkflowEventBridge
{
    void RegisterClient(string clientId);
    void UnregisterClient(string clientId);
    bool HasActiveClients { get; }

    Task PublishEventAsync(WorkflowEvent evt, CancellationToken cancellationToken);
    IAsyncEnumerable<WorkflowEvent> ReadEventsAsync(
        string clientId,
        CancellationToken cancellationToken);
}
