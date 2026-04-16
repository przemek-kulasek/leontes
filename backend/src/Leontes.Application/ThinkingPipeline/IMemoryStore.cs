using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Application.ThinkingPipeline;

public interface IMemoryStore
{
    Task<IReadOnlyList<RelevantMemory>> SearchAsync(
        string query, int limit, CancellationToken cancellationToken);

    Task StoreEpisodicAsync(
        Guid conversationId, string userContent, string assistantResponse,
        CancellationToken cancellationToken);
}
