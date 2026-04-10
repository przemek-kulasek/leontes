namespace Leontes.Domain.Entities;

public sealed class Conversation : Entity
{
    public required string Title { get; set; }
    public DateTime LastMessageAt { get; set; }

    public ICollection<Message> Messages { get; init; } = [];
}
