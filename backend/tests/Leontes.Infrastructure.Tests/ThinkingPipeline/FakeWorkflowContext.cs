using Microsoft.Agents.AI.Workflows;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

/// <summary>
/// Minimal IWorkflowContext fake for unit testing executors.
/// </summary>
internal sealed class FakeWorkflowContext : IWorkflowContext
{
    public List<WorkflowEvent> Events { get; } = [];
    public List<object> SentMessages { get; } = [];

    private readonly Dictionary<string, object?> _state = new();

    public ValueTask AddEventAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(workflowEvent);
        return ValueTask.CompletedTask;
    }

    public ValueTask SendMessageAsync(object message, string? targetId = null, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(message);
        return ValueTask.CompletedTask;
    }

    public ValueTask YieldOutputAsync(object output, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask RequestHaltAsync()
        => ValueTask.CompletedTask;

    public ValueTask<T?> ReadStateAsync<T>(string key, string? scopeName = null, CancellationToken cancellationToken = default)
    {
        if (_state.TryGetValue(key, out var value) && value is T typed)
            return ValueTask.FromResult<T?>(typed);
        return ValueTask.FromResult(default(T));
    }

    public ValueTask<T> ReadOrInitStateAsync<T>(string key, Func<T> initialStateFactory, string? scopeName = null, CancellationToken cancellationToken = default)
    {
        if (_state.TryGetValue(key, out var value) && value is T typed)
            return ValueTask.FromResult(typed);
        var init = initialStateFactory();
        _state[key] = init;
        return ValueTask.FromResult(init);
    }

    public ValueTask<HashSet<string>> ReadStateKeysAsync(string? scopeName = null, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new HashSet<string>(_state.Keys));

    public ValueTask QueueStateUpdateAsync<T>(string key, T? value, string? scopeName = null, CancellationToken cancellationToken = default)
    {
        _state[key] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask QueueClearScopeAsync(string? scopeName = null, CancellationToken cancellationToken = default)
    {
        _state.Clear();
        return ValueTask.CompletedTask;
    }

    public IReadOnlyDictionary<string, string>? TraceContext => null;
    public bool ConcurrentRunsEnabled => false;
}
