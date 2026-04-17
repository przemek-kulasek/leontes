using Leontes.Application.Configuration;
using Leontes.Infrastructure.AI.Memory;
using Microsoft.Extensions.AI;

namespace Leontes.Infrastructure.Tests.AI.Memory;

public sealed class EmbeddingServiceTests
{
    [Fact]
    public async Task EmbedAsync_ValidText_ReturnsVectorWithExpectedDimensions()
    {
        var ct = TestContext.Current.CancellationToken;
        var generator = new FakeEmbeddingGenerator(MemoryOptions.EmbeddingDimensions);
        var sut = new EmbeddingService(generator);

        var vector = await sut.EmbedAsync("hello", ct);

        Assert.Equal(MemoryOptions.EmbeddingDimensions, vector.ToArray().Length);
    }

    [Fact]
    public async Task EmbedAsync_EmptyText_ThrowsArgumentException()
    {
        var ct = TestContext.Current.CancellationToken;
        var generator = new FakeEmbeddingGenerator(MemoryOptions.EmbeddingDimensions);
        var sut = new EmbeddingService(generator);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.EmbedAsync("   ", ct));
    }

    [Fact]
    public async Task EmbedAsync_WrongDimensions_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var generator = new FakeEmbeddingGenerator(128);
        var sut = new EmbeddingService(generator);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EmbedAsync("hi", ct));
        Assert.Contains("dimension", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbedBatchAsync_EmptyList_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var generator = new FakeEmbeddingGenerator(MemoryOptions.EmbeddingDimensions);
        var sut = new EmbeddingService(generator);

        var result = await sut.EmbedBatchAsync([], ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task EmbedBatchAsync_MultipleTexts_ReturnsVectorPerText()
    {
        var ct = TestContext.Current.CancellationToken;
        var generator = new FakeEmbeddingGenerator(MemoryOptions.EmbeddingDimensions);
        var sut = new EmbeddingService(generator);

        var result = await sut.EmbedBatchAsync(["one", "two", "three"], ct);

        Assert.Equal(3, result.Count);
        Assert.All(result, v => Assert.Equal(MemoryOptions.EmbeddingDimensions, v.ToArray().Length));
    }

    private sealed class FakeEmbeddingGenerator(int dimensions)
        : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var embeddings = new GeneratedEmbeddings<Embedding<float>>();
            foreach (var _ in values)
            {
                embeddings.Add(new Embedding<float>(new float[dimensions]));
            }
            return Task.FromResult(embeddings);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
