using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication;

public interface IRequestPortTimeoutScheduler
{
    void Schedule(ExternalRequest request);
    void Cancel(string requestId);
}
