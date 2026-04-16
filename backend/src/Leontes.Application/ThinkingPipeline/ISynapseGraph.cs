using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Application.ThinkingPipeline;

public interface ISynapseGraph
{
    Task<ResolvedEntity?> ResolveAsync(string mention, CancellationToken cancellationToken);

    Task AddRelationshipAsync(
        Guid entityId, string relationType, Guid relatedEntityId,
        CancellationToken cancellationToken);
}
