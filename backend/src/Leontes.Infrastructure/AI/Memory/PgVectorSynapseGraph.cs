using Leontes.Application;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;

namespace Leontes.Infrastructure.AI.Memory;

internal sealed class PgVectorSynapseGraph(
    IServiceScopeFactory scopeFactory,
    IEmbeddingService embeddingService,
    ILogger<PgVectorSynapseGraph> logger) : ISynapseGraph
{
    public async Task<ResolvedEntity?> ResolveAsync(string mention, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mention))
            return null;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var entity = await db.SynapseEntities
            .AsNoTracking()
            .Where(e => EF.Functions.ILike(e.Name, mention))
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is not null)
            return new ResolvedEntity(mention, entity.Id, entity.EntityType.ToString(), entity.Name);

        var matches = await SemanticSearchAsync(mention, limit: 1, cancellationToken);
        if (matches.Count == 0)
            return null;

        var match = matches[0];
        return new ResolvedEntity(mention, match.Id, match.EntityType.ToString(), match.Name);
    }

    public async Task<SynapseEntity?> FindEntityAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        return await db.SynapseEntities
            .AsNoTracking()
            .Where(e => EF.Functions.ILike(e.Name, name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SynapseEntity?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        return await db.SynapseEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken);
    }

    public async Task<IReadOnlyList<RelatedEntity>> FindRelatedEntitiesAsync(
        Guid entityId,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth < 1)
            return [];

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        const string sql = """
            WITH RECURSIVE graph AS (
                SELECT r."TargetEntityId" AS id, r."RelationType" AS relation_type, 1 AS depth
                FROM "SynapseRelationships" r
                WHERE r."SourceEntityId" = {0}

                UNION

                SELECT r."SourceEntityId" AS id, r."RelationType" AS relation_type, 1 AS depth
                FROM "SynapseRelationships" r
                WHERE r."TargetEntityId" = {0}

                UNION ALL

                SELECT r."TargetEntityId" AS id, r."RelationType" AS relation_type, g.depth + 1
                FROM "SynapseRelationships" r
                INNER JOIN graph g ON r."SourceEntityId" = g.id
                WHERE g.depth < {1} AND r."TargetEntityId" <> {0}

                UNION

                SELECT r."SourceEntityId" AS id, r."RelationType" AS relation_type, g.depth + 1
                FROM "SynapseRelationships" r
                INNER JOIN graph g ON r."TargetEntityId" = g.id
                WHERE g.depth < {1} AND r."SourceEntityId" <> {0}
            )
            SELECT DISTINCT ON (e."Id")
                e."Id", e."Name", e."EntityType", g.relation_type, g.depth
            FROM graph g
            INNER JOIN "SynapseEntities" e ON e."Id" = g.id
            WHERE e."Id" <> {0}
            ORDER BY e."Id", g.depth;
            """;

        var rows = await db.Database
            .SqlQueryRaw<RelatedEntityRow>(sql, entityId, depth)
            .ToListAsync(cancellationToken);

        return [.. rows
            .OrderBy(r => r.Depth)
            .Select(r => new RelatedEntity(
                r.Id,
                r.Name,
                Enum.Parse<SynapseEntityType>(r.EntityType),
                r.RelationType,
                r.Depth))];
    }

    public async Task<SynapseEntity> UpsertEntityAsync(
        string name,
        SynapseEntityType type,
        Dictionary<string, string>? properties,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be empty.", nameof(name));

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await db.SynapseEntitySet
            .FirstOrDefaultAsync(e => e.EntityType == type && e.Name == name, cancellationToken);

        if (existing is not null)
        {
            if (properties is not null)
            {
                foreach (var (key, value) in properties)
                    existing.Properties[key] = value;
            }

            await db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var embedding = await embeddingService.EmbedAsync(name, cancellationToken);

        var entity = new SynapseEntity
        {
            Name = name,
            EntityType = type,
            Embedding = embedding,
            Properties = properties ?? []
        };

        db.SynapseEntitySet.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Upserted synapse entity {EntityId} ({Type}/{Name})", entity.Id, type, name);

        return entity;
    }

    public async Task AddRelationshipAsync(
        Guid sourceId,
        Guid targetId,
        string relationType,
        CancellationToken cancellationToken)
    {
        if (sourceId == targetId)
            return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var exists = await db.SynapseRelationshipSet.AnyAsync(
            r => r.SourceEntityId == sourceId
                && r.TargetEntityId == targetId
                && r.RelationType == relationType,
            cancellationToken);

        if (exists)
            return;

        db.SynapseRelationshipSet.Add(new SynapseRelationship
        {
            SourceEntityId = sourceId,
            TargetEntityId = targetId,
            RelationType = relationType
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SynapseEntity>> SemanticSearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
            return [];

        var embedding = await embeddingService.EmbedAsync(query, cancellationToken);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        return await db.SynapseEntities
            .AsNoTracking()
            .Where(e => e.Embedding != null)
            .OrderBy(e => e.Embedding!.CosineDistance(embedding))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private sealed record RelatedEntityRow(Guid Id, string Name, string EntityType, string RelationType, int Depth);
}
