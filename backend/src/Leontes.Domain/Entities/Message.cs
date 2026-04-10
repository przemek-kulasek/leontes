using Leontes.Domain.Enums;

namespace Leontes.Domain.Entities;

public sealed class Message : Entity
{
    public required MessageRole Role { get; init; }
    public required string Content { get; set; }
    public required MessageChannel Channel { get; init; }
    public bool IsComplete { get; set; } = true;

    public Guid ConversationId { get; init; }
    public Conversation Conversation { get; init; } = null!;
}
