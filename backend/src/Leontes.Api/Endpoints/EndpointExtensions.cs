using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Leontes.Api.Endpoints;

public static class EndpointExtensions
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        // Register feature endpoint groups here as they are implemented
        // Example: api.MapChatEndpoints();

        return app;
    }
}
