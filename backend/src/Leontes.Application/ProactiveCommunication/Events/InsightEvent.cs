using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication.Events;

public sealed class InsightEvent(
    string content,
    string source) : WorkflowEvent(new InsightPayload(content, source));

public sealed record InsightPayload(string Content, string Source);
