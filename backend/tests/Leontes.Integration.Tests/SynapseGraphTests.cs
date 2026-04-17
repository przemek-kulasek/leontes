using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.Data;
using Leontes.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Leontes.Integration.Tests;

public sealed class SynapseGraphTests(LeontesApiFactory factory) : IClassFixture<LeontesApiFactory>
{
    private readonly LeontesApiFactory _factory = factory;

    [Fact]
    public async Task FindEntityAsync_ByNameWithDifferentCasing_MatchesCaseInsensitively()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var graph = scope.ServiceProvider.GetRequiredService<ISynapseGraph>();

        var created = await graph.UpsertEntityAsync("Sarah", SynapseEntityType.Person, null, ct);

        var found = await graph.FindEntityAsync("SARAH", ct);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task FindEntityAsync_NameWithUnderscore_DoesNotMatchArbitraryCharacter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var graph = scope.ServiceProvider.GetRequiredService<ISynapseGraph>();

        await graph.UpsertEntityAsync("nodeXjs", SynapseEntityType.Project, null, ct);

        var found = await graph.FindEntityAsync("node_js", ct);

        Assert.Null(found);
    }

    [Fact]
    public async Task FindRelatedEntitiesAsync_DepthOne_ReturnsDirectNeighborsOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var graph = scope.ServiceProvider.GetRequiredService<ISynapseGraph>();

        var (a, b, c) = await SeedChainAsync(graph, ct);

        var related = await graph.FindRelatedEntitiesAsync(a.Id, depth: 1, ct);

        Assert.Contains(related, r => r.Id == b.Id);
        Assert.DoesNotContain(related, r => r.Id == c.Id);
    }

    [Fact]
    public async Task FindRelatedEntitiesAsync_DepthTwo_ReturnsTransitiveNeighbors()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var graph = scope.ServiceProvider.GetRequiredService<ISynapseGraph>();

        var (a, b, c) = await SeedChainAsync(graph, ct);

        var related = await graph.FindRelatedEntitiesAsync(a.Id, depth: 2, ct);

        Assert.Contains(related, r => r.Id == b.Id && r.Depth == 1);
        Assert.Contains(related, r => r.Id == c.Id && r.Depth == 2);
    }

    [Fact]
    public async Task FindRelatedEntitiesAsync_TraversesBothDirections()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var graph = scope.ServiceProvider.GetRequiredService<ISynapseGraph>();

        // Chain: A -> B -> C; query from C must still reach B and A
        var (a, b, c) = await SeedChainAsync(graph, ct);

        var related = await graph.FindRelatedEntitiesAsync(c.Id, depth: 2, ct);

        Assert.Contains(related, r => r.Id == b.Id);
        Assert.Contains(related, r => r.Id == a.Id);
    }

    [Fact]
    public async Task UpsertEntityAsync_DifferentCasing_ReturnsExistingEntity()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var graph = scope.ServiceProvider.GetRequiredService<ISynapseGraph>();

        var name = NewName("Upsert");
        var first = await graph.UpsertEntityAsync(name, SynapseEntityType.Person, null, ct);
        var second = await graph.UpsertEntityAsync(name.ToUpper(), SynapseEntityType.Person, null, ct);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task AddRelationshipAsync_Duplicate_IsNoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var graph = scope.ServiceProvider.GetRequiredService<ISynapseGraph>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var alice = await graph.UpsertEntityAsync(NewName("alice"), SynapseEntityType.Person, null, ct);
        var bob = await graph.UpsertEntityAsync(NewName("bob"), SynapseEntityType.Person, null, ct);

        await graph.AddRelationshipAsync(alice.Id, bob.Id, "KNOWS", ct);
        await graph.AddRelationshipAsync(alice.Id, bob.Id, "KNOWS", ct);

        var count = await db.SynapseRelationshipSet
            .CountAsync(r => r.SourceEntityId == alice.Id && r.TargetEntityId == bob.Id, ct);

        Assert.Equal(1, count);
    }

    private static async Task<(Leontes.Domain.Entities.SynapseEntity A,
        Leontes.Domain.Entities.SynapseEntity B,
        Leontes.Domain.Entities.SynapseEntity C)> SeedChainAsync(
        ISynapseGraph graph, CancellationToken ct)
    {
        var a = await graph.UpsertEntityAsync(NewName("a"), SynapseEntityType.Concept, null, ct);
        var b = await graph.UpsertEntityAsync(NewName("b"), SynapseEntityType.Concept, null, ct);
        var c = await graph.UpsertEntityAsync(NewName("c"), SynapseEntityType.Concept, null, ct);
        await graph.AddRelationshipAsync(a.Id, b.Id, "LINKS", ct);
        await graph.AddRelationshipAsync(b.Id, c.Id, "LINKS", ct);
        return (a, b, c);
    }

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
