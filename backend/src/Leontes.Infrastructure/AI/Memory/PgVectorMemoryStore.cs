using Leontes.Application;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Leontes.Infrastructure.AI.Memory;

internal sealed class PgVectorMemoryStore(
    IServiceScopeFactory scopeFactory,
    IEmbeddingService embeddingService,
    ILogger<PgVectorMemoryStore> logger) : IMemoryStore
{
    public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
            return [];

        var embedding = await embeddingService.EmbedAsync(query, cancellationToken);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var rows = await db.MemoryEntries
            .AsNoTracking()
            .OrderBy(m => m.Embedding.CosineDistance(embedding))
            .Take(limit)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.Type,
                m.Created,
                Distance = m.Embedding.CosineDistance(embedding)
            })
            .ToListAsync(cancellationToken);

        logger.LogDebug(
            "Memory search '{Query}' returned {Count} results",
            query, rows.Count);

        return [.. rows.Select(r => new MemorySearchResult(
            r.Id,
            r.Content,
            r.Type,
            1.0 - r.Distance,
            r.Created))];
    }

    public async Task<Guid> StoreAsync(
        string content,
        MemoryType type,
        Guid? sourceMessageId,
        Guid? sourceConversationId,
        float importance,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content must not be empty.", nameof(content));

        var embedding = await embeddingService.EmbedAsync(content, cancellationToken);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var entry = new MemoryEntry
        {
            Content = content,
            Embedding = embedding,
            Type = type,
            SourceMessageId = sourceMessageId,
            SourceConversationId = sourceConversationId,
            Importance = importance
        };

        db.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogDebug(
            "Stored memory {MemoryId} of type {Type} (importance {Importance})",
            entry.Id, type, importance);

        return entry.Id;
    }
}
