using Leontes.Domain.Enums;

namespace Leontes.Application.ThinkingPipeline;

public interface IMemoryStore
{
    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);

    Task<Guid> StoreAsync(
        string content,
        MemoryType type,
        Guid? sourceMessageId,
        Guid? sourceConversationId,
        float importance,
        CancellationToken cancellationToken);
}

public sealed record MemorySearchResult(
    Guid Id,
    string Content,
    MemoryType Type,
    double Relevance,
    DateTime CreatedAt);
