using System.Text.Json;
using Leontes.Infrastructure.AI.ThinkingPipeline;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class InMemoryCheckpointStoreTests
{
    private readonly InMemoryCheckpointStore _store = new();

    [Fact]
    public async Task CreateAndRetrieve_RoundTrips()
    {
        var data = JsonSerializer.SerializeToElement(new { foo = "bar" });

        var info = await _store.CreateCheckpointAsync("session-1", data);
        var retrieved = await _store.RetrieveCheckpointAsync("session-1", info);

        Assert.Equal("bar", retrieved.GetProperty("foo").GetString());
    }

    [Fact]
    public async Task RetrieveCheckpoint_UnknownSession_Throws()
    {
        var info = new Microsoft.Agents.AI.Workflows.CheckpointInfo("missing", "cp-0");

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _store.RetrieveCheckpointAsync("missing", info).AsTask());
    }

    [Fact]
    public async Task RetrieveIndex_NoParentFilter_ReturnsAll()
    {
        var data = JsonSerializer.SerializeToElement(new { });

        var cp1 = await _store.CreateCheckpointAsync("s1", data);
        var cp2 = await _store.CreateCheckpointAsync("s1", data, parent: cp1);

        var index = await _store.RetrieveIndexAsync("s1");

        Assert.Equal(2, index.Count());
    }

    [Fact]
    public async Task RetrieveIndex_WithParentFilter_ReturnsOnlyChildren()
    {
        var data = JsonSerializer.SerializeToElement(new { });

        var root = await _store.CreateCheckpointAsync("s1", data);
        await _store.CreateCheckpointAsync("s1", data, parent: root);
        await _store.CreateCheckpointAsync("s1", data, parent: root);
        await _store.CreateCheckpointAsync("s1", data); // no parent

        var children = await _store.RetrieveIndexAsync("s1", withParent: root);

        Assert.Equal(2, children.Count());
    }

    [Fact]
    public async Task RetrieveIndex_UnknownSession_ReturnsEmpty()
    {
        var index = await _store.RetrieveIndexAsync("nonexistent");

        Assert.Empty(index);
    }
}
