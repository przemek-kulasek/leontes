using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Enums;
using Leontes.Domain.Exceptions;

namespace Leontes.Api.Endpoints;

public static class SynapseEndpoints
{
    public static RouteGroupBuilder MapSynapseEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/synapse/entities/{entityId:guid}/related", GetRelatedEntities)
            .WithName("GetRelatedSynapseEntities")
            .WithSummary("Return a Synapse Graph entity and its neighbors up to the given depth")
            .WithTags("Synapse")
            .Produces<RelatedEntitiesResponse>()
            .Produces(400)
            .Produces(404);

        return group;
    }

    private static async Task<IResult> GetRelatedEntities(
        ISynapseGraph synapseGraph,
        Guid entityId,
        int depth = 2,
        CancellationToken cancellationToken = default)
    {
        if (depth < 1 || depth > 5)
            throw new ValidationException("'depth' must be between 1 and 5.");

        var entity = await synapseGraph.GetEntityAsync(entityId, cancellationToken)
            ?? throw new NotFoundException($"Synapse entity {entityId} was not found.");

        var related = await synapseGraph.FindRelatedEntitiesAsync(entityId, depth, cancellationToken);

        return Results.Ok(new RelatedEntitiesResponse(
            new EntityDto(entity.Id, entity.Name, entity.EntityType, entity.Properties),
            [.. related.Select(r => new RelatedEntityDto(r.Id, r.Name, r.EntityType, r.RelationType, r.Depth))]));
    }

    public sealed record RelatedEntitiesResponse(EntityDto Entity, IReadOnlyList<RelatedEntityDto> Related);

    public sealed record EntityDto(
        Guid Id,
        string Name,
        SynapseEntityType EntityType,
        IReadOnlyDictionary<string, string> Properties);

    public sealed record RelatedEntityDto(
        Guid Id,
        string Name,
        SynapseEntityType EntityType,
        string RelationType,
        int Depth);
}
