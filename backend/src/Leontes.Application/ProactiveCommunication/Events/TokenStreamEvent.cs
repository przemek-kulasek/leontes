using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication.Events;

public sealed class TokenStreamEvent(string text) : WorkflowEvent(text)
{
    public string Text { get; } = text;
}
