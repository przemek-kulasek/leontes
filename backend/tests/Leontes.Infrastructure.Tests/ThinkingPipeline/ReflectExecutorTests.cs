using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Executors;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class ReflectExecutorTests
{
    [Fact]
    public async Task HandleAsync_IncompleteResponse_SkipsReflection()
    {
        var ct = TestContext.Current.CancellationToken;
        var memoryStore = new RecordingMemoryStore();
        var executor = CreateExecutor(memoryStore);
        var context = CreateContext();
        context.Response = "partial";
        context.IsComplete = false;
        var workflowContext = new FakeWorkflowContext();

        var result = await executor.HandleAsync(context, workflowContext, ct);

        Assert.False(result.IsComplete);
        Assert.Empty(result.NewInsights);
        Assert.Empty(memoryStore.Stored);
    }

    [Fact]
    public async Task HandleAsync_CompleteResponse_StoresObservationMemory()
    {
        var ct = TestContext.Current.CancellationToken;
        var memoryStore = new RecordingMemoryStore();
        var executor = CreateExecutor(memoryStore);
        var context = CreateContext();
        context.Response = "Hello back.";
        context.IsComplete = true;
        var workflowContext = new FakeWorkflowContext();

        await executor.HandleAsync(context, workflowContext, ct);

        Assert.Contains(memoryStore.Stored, s => s.Type == MemoryType.Observation);
        var observation = memoryStore.Stored.First(s => s.Type == MemoryType.Observation);
        Assert.Contains(context.UserContent, observation.Content);
        Assert.Contains(context.Response, observation.Content);
        Assert.Equal(context.ConversationId, observation.SourceConversationId);
    }

    [Fact]
    public async Task HandleAsync_CompleteResponse_ExtractsInsights()
    {
        var ct = TestContext.Current.CancellationToken;
        var memoryStore = new RecordingMemoryStore();
        var executor = CreateExecutor(memoryStore);
        var context = CreateContext();
        context.Response = "Here are your files.";
        context.IsComplete = true;
        context.Intent = "search";
        context.ToolResults =
        [
            new ToolCallResult("file_search", "*.pdf", "Found 3 files", true)
        ];
        var workflowContext = new FakeWorkflowContext();

        var result = await executor.HandleAsync(context, workflowContext, ct);

        Assert.NotEmpty(result.NewInsights);
        Assert.Contains(memoryStore.Stored, s => s.Type == MemoryType.Insight);
    }

    [Fact]
    public async Task HandleAsync_StoresInsightsWithHigherImportanceThanObservations()
    {
        var ct = TestContext.Current.CancellationToken;
        var memoryStore = new RecordingMemoryStore();
        var executor = CreateExecutor(memoryStore);
        var context = CreateContext();
        context.Response = "Done.";
        context.IsComplete = true;
        context.Intent = "search";
        context.ResolvedEntities =
        [
            new ResolvedEntity("john", Guid.NewGuid(), "Person", "John Smith")
        ];
        context.ToolResults =
        [
            new ToolCallResult("file_search", "*.pdf", "Found", true)
        ];
        var workflowContext = new FakeWorkflowContext();

        await executor.HandleAsync(context, workflowContext, ct);

        var observation = memoryStore.Stored.First(s => s.Type == MemoryType.Observation);
        var insight = memoryStore.Stored.FirstOrDefault(s => s.Type == MemoryType.Insight);

        Assert.NotNull(insight);
        Assert.True(insight.Importance > observation.Importance);
    }

    [Fact]
    public async Task HandleAsync_CompleteWithInsights_EmitsInsightEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var executor = CreateExecutor(new RecordingMemoryStore());
        var context = CreateContext();
        context.Response = "Done.";
        context.IsComplete = true;
        context.ResolvedEntities =
        [
            new ResolvedEntity("john", Guid.NewGuid(), "Person", "John Smith")
        ];
        var workflowContext = new FakeWorkflowContext();

        await executor.HandleAsync(context, workflowContext, ct);

        Assert.Contains(workflowContext.Events, e => e is InsightEvent);
    }

    [Fact]
    public async Task HandleAsync_CompleteWithNoInsights_DoesNotEmitInsightEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var executor = CreateExecutor(new RecordingMemoryStore());
        var context = CreateContext();
        context.Response = "Hello!";
        context.IsComplete = true;
        var workflowContext = new FakeWorkflowContext();

        await executor.HandleAsync(context, workflowContext, ct);

        Assert.DoesNotContain(workflowContext.Events, e => e is InsightEvent);
    }

    [Fact]
    public async Task HandleAsync_EmitsProgressEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var executor = CreateExecutor(new RecordingMemoryStore());
        var context = CreateContext();
        context.Response = "Done.";
        context.IsComplete = true;
        var workflowContext = new FakeWorkflowContext();

        await executor.HandleAsync(context, workflowContext, ct);

        Assert.Contains(workflowContext.Events, e => e is ProgressEvent);
    }

    [Fact]
    public async Task HandleAsync_MemoryStoreThrows_DoesNotPropagate()
    {
        var ct = TestContext.Current.CancellationToken;
        var memoryStore = new ThrowingMemoryStore();
        var executor = CreateExecutor(memoryStore);
        var context = CreateContext();
        context.Response = "Done.";
        context.IsComplete = true;
        var workflowContext = new FakeWorkflowContext();

        var result = await executor.HandleAsync(context, workflowContext, ct);

        Assert.Equal(context.MessageId, result.MessageId);
    }

    private static ReflectExecutor CreateExecutor(IMemoryStore memoryStore) => new(
        memoryStore,
        new NullSynapseGraph(),
        new NullDecisionRecorder(),
        NullLogger<ReflectExecutor>.Instance);

    private static ThinkingContext CreateContext() => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = "test message",
        Channel = "Cli"
    };

    private sealed record StoredMemory(
        string Content, MemoryType Type, Guid? SourceMessageId,
        Guid? SourceConversationId, float Importance);

    private sealed class RecordingMemoryStore : IMemoryStore
    {
        public List<StoredMemory> Stored { get; } = [];

        public Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
            string query, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MemorySearchResult>>([]);

        public Task<Guid> StoreAsync(
            string content, MemoryType type, Guid? sourceMessageId,
            Guid? sourceConversationId, float importance,
            CancellationToken cancellationToken)
        {
            Stored.Add(new StoredMemory(content, type, sourceMessageId, sourceConversationId, importance));
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class ThrowingMemoryStore : IMemoryStore
    {
        public Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
            string query, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MemorySearchResult>>([]);

        public Task<Guid> StoreAsync(
            string content, MemoryType type, Guid? sourceMessageId,
            Guid? sourceConversationId, float importance,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Store failed");
    }
}
