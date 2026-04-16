using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication.Events;

public sealed class ProgressEvent(
    string stage,
    string description,
    double? progress) : WorkflowEvent(new ProgressPayload(stage, description, progress));

public sealed record ProgressPayload(string Stage, string Description, double? Progress);
