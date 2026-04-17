using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Application.ThinkingPipeline;

public interface ISynapseGraph
{
    Task<ResolvedEntity?> ResolveAsync(string mention, CancellationToken cancellationToken);

    Task<SynapseEntity?> FindEntityAsync(string name, CancellationToken cancellationToken);

    Task<SynapseEntity?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RelatedEntity>> FindRelatedEntitiesAsync(
        Guid entityId,
        int depth,
        CancellationToken cancellationToken);

    Task<SynapseEntity> UpsertEntityAsync(
        string name,
        SynapseEntityType type,
        Dictionary<string, string>? properties,
        CancellationToken cancellationToken);

    Task AddRelationshipAsync(
        Guid sourceId,
        Guid targetId,
        string relationType,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SynapseEntity>> SemanticSearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}

public sealed record RelatedEntity(
    Guid Id,
    string Name,
    SynapseEntityType EntityType,
    string RelationType,
    int Depth);
