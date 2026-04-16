using Leontes.Domain.Enums;

namespace Leontes.Domain.Entities;

public sealed class Conversation : Entity
{
    public required string Title { get; set; }
    public DateTime LastMessageAt { get; set; }
    public MessageInitiator InitiatedBy { get; set; } = MessageInitiator.User;
    public bool IsProactive { get; set; }

    public ICollection<Message> Messages { get; init; } = [];
}
