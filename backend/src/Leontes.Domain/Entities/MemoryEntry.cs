using Leontes.Domain.Enums;
using Pgvector;

namespace Leontes.Domain.Entities;

public sealed class MemoryEntry : Entity
{
    public required string Content { get; set; }
    public required Vector Embedding { get; set; }
    public required MemoryType Type { get; set; }
    public Guid? SourceMessageId { get; set; }
    public Guid? SourceConversationId { get; set; }
    public float Importance { get; set; } = 0.5f;
}
