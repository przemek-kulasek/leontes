using System.Runtime.CompilerServices;
using Leontes.Application;
using Leontes.Application.Configuration;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class ContextWindowManagerTests
{
    private static ContextWindowManager Build(out FakeSummarizer summarizer, out FakeDb db)
    {
        summarizer = new FakeSummarizer();
        db = new FakeDb();
        var scopeFactory = new FakeScopeFactory(db);
        var options = Options.Create(new ResilienceOptions
        {
            ContextWindow = new ContextWindowOptions
            {
                BufferPercentage = 10,
                MinRecentTurns = 2,
                AverageCharsPerToken = 1
            }
        });
        return new ContextWindowManager(summarizer, scopeFactory, options, NullLogger<ContextWindowManager>.Instance);
    }

    private static ThinkingContext Ctx(IReadOnlyList<HistoryMessage> history, IReadOnlyList<RelevantMemory>? memories = null) =>
        new()
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            UserContent = "hi",
            Channel = "Cli",
            ConversationHistory = history,
            RelevantMemories = memories ?? []
        };

    [Fact]
    public async Task FitAsync_UnderBudget_ReturnsContextUnchanged()
    {
        var manager = Build(out _, out var db);
        var ctx = Ctx([new("User", "short", DateTime.UtcNow)]);

        var result = await manager.FitAsync(ctx, modelTokenLimit: 1000, TestContext.Current.CancellationToken);

        Assert.Same(ctx, result);
        Assert.Single(result.ConversationHistory);
        Assert.Empty(db.Added);
    }

    [Fact]
    public async Task FitAsync_OverBudget_DropsLowRelevanceMemoriesFirst()
    {
        var manager = Build(out _, out _);
        var memories = new[]
        {
            new RelevantMemory(Guid.NewGuid(), new string('a', 200), MemoryType.Fact, 0.1, DateTime.UtcNow),
            new RelevantMemory(Guid.NewGuid(), new string('b', 200), MemoryType.Fact, 0.9, DateTime.UtcNow)
        };
        var ctx = Ctx([new("User", "hi", DateTime.UtcNow)], memories);

        var result = await manager.FitAsync(ctx, modelTokenLimit: 250, TestContext.Current.CancellationToken);

        Assert.Single(result.RelevantMemories);
        Assert.Equal(0.9, result.RelevantMemories[0].Relevance);
    }

    [Fact]
    public async Task FitAsync_HistoryTooLong_SummarizesOlderAndPersists()
    {
        var manager = Build(out var summarizer, out var db);
        var history = Enumerable.Range(0, 6)
            .Select(i => new HistoryMessage("User", new string((char)('a' + i), 300), DateTime.UtcNow.AddMinutes(i)))
            .ToList();
        var ctx = Ctx(history);

        var result = await manager.FitAsync(ctx, modelTokenLimit: 900, TestContext.Current.CancellationToken);

        Assert.Equal(1, summarizer.Calls);
        Assert.Single(db.Added);
        var stored = (Message)db.Added[0];
        Assert.Equal(MessageRole.Summary, stored.Role);
        Assert.StartsWith("[summary", result.ConversationHistory[0].Content);
    }

    [Fact]
    public async Task FitAsync_StillOverAfterSummary_TruncatesOldest()
    {
        var manager = Build(out _, out _);
        var history = Enumerable.Range(0, 4)
            .Select(i => new HistoryMessage("User", new string('x', 500), DateTime.UtcNow.AddMinutes(i)))
            .ToList();
        var ctx = Ctx(history);

        var result = await manager.FitAsync(ctx, modelTokenLimit: 600, TestContext.Current.CancellationToken);

        Assert.True(result.ConversationHistory.Count < 4);
    }

    private sealed class FakeSummarizer : IChatClient
    {
        public int Calls;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "summary text")]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Empty(cancellationToken);

        private static async IAsyncEnumerable<ChatResponseUpdate> Empty(
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class FakeScopeFactory(FakeDb db) : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IApplicationDbContext) ? db : null;
        public void Dispose() { }
    }

    private sealed class FakeDb : IApplicationDbContext
    {
        public List<object> Added { get; } = [];
        public IQueryable<Conversation> Conversations => Array.Empty<Conversation>().AsQueryable();
        public IQueryable<Message> Messages => Array.Empty<Message>().AsQueryable();
        public IQueryable<StoredProactiveEvent> StoredProactiveEvents => Array.Empty<StoredProactiveEvent>().AsQueryable();
        public IQueryable<MemoryEntry> MemoryEntries => Array.Empty<MemoryEntry>().AsQueryable();
        public IQueryable<SynapseEntity> SynapseEntities => Array.Empty<SynapseEntity>().AsQueryable();
        public IQueryable<SynapseRelationship> SynapseRelationships => Array.Empty<SynapseRelationship>().AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class => Added.Add(entity);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
