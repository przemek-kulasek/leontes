using Leontes.Domain.Enums;
using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication.Events;

public sealed class NotificationEvent(
    string title,
    string content,
    ProactiveUrgency urgency) : WorkflowEvent(new NotificationPayload(title, content, urgency));

public sealed record NotificationPayload(string Title, string Content, ProactiveUrgency Urgency);
