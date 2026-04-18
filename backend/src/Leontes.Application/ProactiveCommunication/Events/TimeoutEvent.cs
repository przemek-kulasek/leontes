using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication.Events;

/// <summary>
/// Emitted when a RequestPort expires without a response (feature 85).
/// </summary>
public sealed class TimeoutEvent : WorkflowEvent
{
    public TimeoutEvent(string requestId, string requestType, string appliedDefault)
        : base(new TimeoutPayload(requestId, requestType, appliedDefault))
    {
    }
}

public sealed record TimeoutPayload(
    string RequestId,
    string RequestType,
    string AppliedDefault);
