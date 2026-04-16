using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

internal sealed class NullMemoryStore : IMemoryStore
{
    public Task<IReadOnlyList<RelevantMemory>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<RelevantMemory>>([]);
    }

    public Task StoreEpisodicAsync(
        Guid conversationId, string userContent, string assistantResponse,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
