using Leontes.Application.Configuration;
using Leontes.Application.ThinkingPipeline;
using Pgvector;

namespace Leontes.Integration.Tests.Infrastructure;

/// <summary>
/// Deterministic embedding for integration tests — hashes the text into a fixed-dimension vector
/// so identical inputs produce identical vectors without requiring a running embedding server.
/// </summary>
internal sealed class FakeEmbeddingService : IEmbeddingService
{
    public Task<Vector> EmbedAsync(string text, CancellationToken cancellationToken)
        => Task.FromResult(Embed(text));

    public Task<IReadOnlyList<Vector>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Vector>>([.. texts.Select(Embed)]);

    private static Vector Embed(string text)
    {
        var values = new float[MemoryOptions.EmbeddingDimensions];
        var seed = text.GetHashCode();
        var rng = new Random(seed);
        for (var i = 0; i < values.Length; i++)
            values[i] = (float)(rng.NextDouble() - 0.5);
        return new Vector(values);
    }
}
