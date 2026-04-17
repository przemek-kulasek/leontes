using Leontes.Application.ThinkingPipeline;

namespace Leontes.Api.Endpoints;

public static class MemoryEndpoints
{
    public static RouteGroupBuilder MapMemoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/memory/search", SearchMemory)
            .WithName("SearchMemory")
            .WithSummary("Search stored memories using semantic similarity")
            .WithTags("Memory")
            .Produces<IReadOnlyList<MemorySearchResult>>()
            .Produces(400);

        return group;
    }

    private static async Task<IResult> SearchMemory(
        IMemoryStore memoryStore,
        string? q,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            throw new Leontes.Domain.Exceptions.ValidationException("Query parameter 'q' is required.");

        if (limit <= 0 || limit > 100)
            throw new Leontes.Domain.Exceptions.ValidationException("'limit' must be between 1 and 100.");

        var results = await memoryStore.SearchAsync(q, limit, cancellationToken);
        return Results.Ok(results);
    }
}
