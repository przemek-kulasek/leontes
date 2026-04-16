using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

/// <summary>
/// In-memory checkpoint store for development. Will be replaced with PostgreSQL
/// when persistent checkpointing is needed.
/// </summary>
internal sealed class InMemoryCheckpointStore : JsonCheckpointStore
{
    private sealed record Entry(CheckpointInfo Info, JsonElement Value, CheckpointInfo? Parent);

    private readonly ConcurrentDictionary<string, List<Entry>> _store = new();

    public override ValueTask<CheckpointInfo> CreateCheckpointAsync(
        string sessionId, JsonElement value, CheckpointInfo? parent = null)
    {
        var entries = _store.GetOrAdd(sessionId, _ => []);

        CheckpointInfo info;
        lock (entries)
        {
            info = new CheckpointInfo(sessionId, $"{sessionId}-{entries.Count}");
            entries.Add(new Entry(info, value, parent));
        }

        return ValueTask.FromResult(info);
    }

    public override ValueTask<JsonElement> RetrieveCheckpointAsync(
        string sessionId, CheckpointInfo key)
    {
        if (_store.TryGetValue(sessionId, out var entries))
        {
            lock (entries)
            {
                foreach (var entry in entries)
                {
                    if (entry.Info.CheckpointId == key.CheckpointId)
                        return ValueTask.FromResult(entry.Value);
                }
            }
        }

        throw new KeyNotFoundException(
            $"Checkpoint '{key.CheckpointId}' not found for session '{sessionId}'");
    }

    public override ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(
        string sessionId, CheckpointInfo? withParent = null)
    {
        if (_store.TryGetValue(sessionId, out var entries))
        {
            lock (entries)
            {
                IEnumerable<CheckpointInfo> result = withParent is not null
                    ? entries
                        .Where(e => e.Parent?.CheckpointId == withParent.CheckpointId)
                        .Select(e => e.Info)
                        .ToList()
                    : entries.Select(e => e.Info).ToList();

                return ValueTask.FromResult(result);
            }
        }

        return ValueTask.FromResult<IEnumerable<CheckpointInfo>>([]);
    }
}
