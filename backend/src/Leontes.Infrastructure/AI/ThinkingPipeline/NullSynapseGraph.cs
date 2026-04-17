using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

internal sealed class NullSynapseGraph : ISynapseGraph
{
    public Task<ResolvedEntity?> ResolveAsync(string mention, CancellationToken cancellationToken)
        => Task.FromResult<ResolvedEntity?>(null);

    public Task<SynapseEntity?> FindEntityAsync(string name, CancellationToken cancellationToken)
        => Task.FromResult<SynapseEntity?>(null);

    public Task<SynapseEntity?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken)
        => Task.FromResult<SynapseEntity?>(null);

    public Task<IReadOnlyList<RelatedEntity>> FindRelatedEntitiesAsync(
        Guid entityId, int depth, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RelatedEntity>>([]);

    public Task<SynapseEntity> UpsertEntityAsync(
        string name,
        SynapseEntityType type,
        Dictionary<string, string>? properties,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("NullSynapseGraph does not support upsert.");

    public Task AddRelationshipAsync(
        Guid sourceId, Guid targetId, string relationType, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<IReadOnlyList<SynapseEntity>> SemanticSearchAsync(
        string query, int limit, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SynapseEntity>>([]);
}
