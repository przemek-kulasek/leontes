using Leontes.Application;
using Leontes.Application.Configuration;
using Leontes.Application.Messaging;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.ProactiveCommunication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests.ProactiveCommunication;

public sealed class ResilientChannelDeliveryTests
{
    private static (ResilientChannelDelivery Delivery, FakeDb Db, FakeClient Signal, FakeClient Cli) Build(
        Func<Exception?>? signalBehavior = null,
        Func<Exception?>? cliBehavior = null,
        int retries = 1,
        int retryDelay = 0)
    {
        var signal = new FakeClient(MessageChannel.Signal, signalBehavior);
        var cli = new FakeClient(MessageChannel.Cli, cliBehavior);
        var db = new FakeDb();
        var options = Options.Create(new ResilienceOptions
        {
            ChannelDelivery = new ChannelDeliveryOptions
            {
                MaxRetries = retries,
                RetryDelaySeconds = retryDelay == 0 ? 1 : retryDelay
            }
        });
        var delivery = new ResilientChannelDelivery(
            [signal, cli], db, options, NullLogger<ResilientChannelDelivery>.Instance);
        return (delivery, db, signal, cli);
    }

    [Fact]
    public async Task DeliverAsync_PreferredSucceeds_ReportsDelivered()
    {
        var (delivery, db, signal, _) = Build();

        var result = await delivery.DeliverAsync(
            new OutboundMessage("user", "hi", MessageChannel.Signal),
            TestContext.Current.CancellationToken);

        Assert.True(result.Delivered);
        Assert.Null(result.FallbackUsed);
        Assert.Equal(1, signal.Sent);
        Assert.Empty(db.Added);
    }

    [Fact]
    public async Task DeliverAsync_PreferredFails_FallsBackToCli()
    {
        var (delivery, db, _, cli) = Build(
            signalBehavior: () => new InvalidOperationException("boom"));

        var result = await delivery.DeliverAsync(
            new OutboundMessage("user", "hi", MessageChannel.Signal),
            TestContext.Current.CancellationToken);

        Assert.True(result.Delivered);
        Assert.Equal(MessageChannel.Cli, result.FallbackUsed);
        Assert.True(cli.Sent > 0);
        Assert.Empty(db.Added);
    }

    [Fact]
    public async Task DeliverAsync_BothFail_QueuesOffline()
    {
        var (delivery, db, _, _) = Build(
            signalBehavior: () => new InvalidOperationException("boom"),
            cliBehavior: () => new InvalidOperationException("nope"));

        var result = await delivery.DeliverAsync(
            new OutboundMessage("user", "hi", MessageChannel.Signal),
            TestContext.Current.CancellationToken);

        Assert.False(result.Delivered);
        Assert.Single(db.Added);
        var stored = (StoredProactiveEvent)db.Added[0];
        Assert.Equal("OfflineDelivery", stored.EventType);
        Assert.Equal(ProactiveEventStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task DeliverAsync_NoClientForChannel_QueuesImmediately()
    {
        var (delivery, db, _, _) = Build();

        var result = await delivery.DeliverAsync(
            new OutboundMessage("user", "hi", MessageChannel.Telegram),
            TestContext.Current.CancellationToken);

        Assert.False(result.Delivered);
        Assert.Single(db.Added);
    }

    private sealed class FakeClient(MessageChannel channel, Func<Exception?>? behavior) : IMessagingClient
    {
        public int Sent;
        public MessageChannel Channel => channel;

        public Task<IReadOnlyList<IncomingMessage>> ReceiveMessagesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IncomingMessage>>([]);

        public Task SendMessageAsync(string recipient, string message, CancellationToken cancellationToken)
        {
            Sent++;
            var ex = behavior?.Invoke();
            if (ex is not null) throw ex;
            return Task.CompletedTask;
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);
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
