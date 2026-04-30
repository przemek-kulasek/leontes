using HealthChecks.UI.Client;
using Leontes.Api.HealthChecks;

namespace Leontes.Api.Extensions;

public static class HealthCheckExtensions
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is required.");

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "database")
            .AddCheck<LlmProviderHealthCheck>("llm-provider")
            .AddCheck<ProcessingQueueHealthCheck>("processing-queue");

        return services;
    }

    public static WebApplication MapApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/_health", new()
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });
        return app;
    }
}
