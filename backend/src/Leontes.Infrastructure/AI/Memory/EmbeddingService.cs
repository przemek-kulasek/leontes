using Leontes.Application.Configuration;
using Leontes.Application.ThinkingPipeline;
using Microsoft.Extensions.AI;
using Pgvector;

namespace Leontes.Infrastructure.AI.Memory;

internal sealed class EmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> generator) : IEmbeddingService
{
    public async Task<Vector> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be empty.", nameof(text));

        var result = await generator.GenerateAsync([text], cancellationToken: cancellationToken);
        var values = result[0].Vector.ToArray();
        EnsureDimensions(values);
        return new Vector(values);
    }

    public async Task<IReadOnlyList<Vector>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
            return [];

        var result = await generator.GenerateAsync(texts, cancellationToken: cancellationToken);
        var vectors = new Vector[result.Count];
        for (var i = 0; i < result.Count; i++)
        {
            var values = result[i].Vector.ToArray();
            EnsureDimensions(values);
            vectors[i] = new Vector(values);
        }
        return vectors;
    }

    private static void EnsureDimensions(float[] values)
    {
        if (values.Length != MemoryOptions.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding dimension mismatch: expected {MemoryOptions.EmbeddingDimensions}, got {values.Length}. " +
                $"Configure an embedding model that matches the schema dimension.");
        }
    }
}
