using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Enums;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

internal sealed class NullMemoryStore : IMemoryStore
{
    public Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MemorySearchResult>>([]);
    }

    public Task<Guid> StoreAsync(
        string content,
        MemoryType type,
        Guid? sourceMessageId,
        Guid? sourceConversationId,
        float importance,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Guid.Empty);
    }
}
