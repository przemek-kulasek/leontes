namespace Leontes.Api.Extensions;

public static class DiagnosticsExtensions
{
    public static IServiceCollection AddApiDiagnostics(this IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddProblemDetails();
        services.AddExceptionHandler<ExceptionHandler>();

        return services;
    }

    public static WebApplication UseApiDiagnostics(this WebApplication app)
    {
        app.UseExceptionHandler();
        return app;
    }
}
