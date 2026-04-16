namespace Leontes.Api.Extensions;

public static class CorsExtensions
{
    private const string PolicyName = "LeontesPolicy";

    public static IServiceCollection AddApiCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy
                    .WithOrigins("http://localhost:5000", "http://localhost:3000")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static WebApplication UseApiCors(this WebApplication app)
    {
        app.UseCors(PolicyName);
        return app;
    }
}
