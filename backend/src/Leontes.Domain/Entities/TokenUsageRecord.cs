namespace Leontes.Domain.Entities;

public sealed class TokenUsageRecord : Entity
{
    public required string Feature { get; init; }
    public required string Operation { get; init; }
    public required string ModelId { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public required DateTime Timestamp { get; init; }
}
