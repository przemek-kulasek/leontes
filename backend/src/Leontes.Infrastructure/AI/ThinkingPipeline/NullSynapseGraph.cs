using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

internal sealed class NullSynapseGraph : ISynapseGraph
{
    public Task<ResolvedEntity?> ResolveAsync(string mention, CancellationToken cancellationToken)
    {
        return Task.FromResult<ResolvedEntity?>(null);
    }

    public Task AddRelationshipAsync(
        Guid entityId, string relationType, Guid relatedEntityId,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
