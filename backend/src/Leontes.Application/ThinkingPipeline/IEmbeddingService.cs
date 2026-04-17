using Pgvector;

namespace Leontes.Application.ThinkingPipeline;

public interface IEmbeddingService
{
    Task<Vector> EmbedAsync(string text, CancellationToken cancellationToken);

    Task<IReadOnlyList<Vector>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken);
}
