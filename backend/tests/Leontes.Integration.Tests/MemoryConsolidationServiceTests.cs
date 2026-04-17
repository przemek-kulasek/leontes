using Leontes.Application;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.AI.Memory;
using Leontes.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Leontes.Integration.Tests;

public sealed class MemoryConsolidationServiceTests(LeontesApiFactory factory)
    : IClassFixture<LeontesApiFactory>
{
    private readonly LeontesApiFactory _factory = factory;

    [Fact]
    public async Task ConsolidateAsync_FewerThanTwoObservations_DoesNotStoreInsights()
    {
        var ct = TestContext.Current.CancellationToken;
        var conversationId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IMemoryStore>();
            await store.StoreAsync("User asked about X", MemoryType.Observation,
                sourceMessageId: null, sourceConversationId: conversationId,
                importance: 0.5f, cancellationToken: ct);
        }

        var service = _factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MemoryConsolidationService>()
            .Single();
        await service.ConsolidateAsync(TimeSpan.FromHours(1), ct);

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var insights = await db.MemoryEntries
            .AsNoTracking()
            .Where(m => m.Type == MemoryType.Insight && m.SourceConversationId == conversationId)
            .CountAsync(ct);

        Assert.Equal(0, insights);
    }

    [Fact]
    public async Task ConsolidateAsync_TwoObservationsInSameConversation_StoresInsights()
    {
        var ct = TestContext.Current.CancellationToken;
        var conversationId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IMemoryStore>();
            await store.StoreAsync("User asked about PostgreSQL migrations",
                MemoryType.Observation, null, conversationId, 0.5f, ct);
            await store.StoreAsync("User asked about EF Core migrations",
                MemoryType.Observation, null, conversationId, 0.5f, ct);
        }

        var service = _factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MemoryConsolidationService>()
            .Single();
        await service.ConsolidateAsync(TimeSpan.FromHours(1), ct);

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var insights = await db.MemoryEntries
            .AsNoTracking()
            .Where(m => m.Type == MemoryType.Insight && m.SourceConversationId == conversationId)
            .ToListAsync(ct);

        Assert.NotEmpty(insights);
        Assert.All(insights, i => Assert.True(i.Importance > 0.5f));
    }

    [Fact]
    public async Task ConsolidateAsync_IgnoresConversationsWithSingleObservation()
    {
        var ct = TestContext.Current.CancellationToken;
        var crowded = Guid.NewGuid();
        var lonely = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IMemoryStore>();
            await store.StoreAsync("Obs crowded 1", MemoryType.Observation, null, crowded, 0.5f, ct);
            await store.StoreAsync("Obs crowded 2", MemoryType.Observation, null, crowded, 0.5f, ct);
            await store.StoreAsync("Obs lonely", MemoryType.Observation, null, lonely, 0.5f, ct);
        }

        var service = _factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MemoryConsolidationService>()
            .Single();
        await service.ConsolidateAsync(TimeSpan.FromHours(1), ct);

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var lonelyInsights = await db.MemoryEntries
            .AsNoTracking()
            .CountAsync(m => m.Type == MemoryType.Insight && m.SourceConversationId == lonely, ct);

        Assert.Equal(0, lonelyInsights);
    }
}
