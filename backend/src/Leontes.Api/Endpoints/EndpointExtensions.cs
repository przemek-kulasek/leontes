namespace Leontes.Api.Endpoints;

public static class EndpointExtensions
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1")
            .RequireAuthorization();

        api.MapChatEndpoints();
        api.MapStreamEndpoints();
        api.MapMemoryEndpoints();
        api.MapSynapseEndpoints();

        return app;
    }
}
