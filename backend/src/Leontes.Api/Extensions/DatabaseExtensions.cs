namespace Leontes.Api.Extensions;

public static class DatabaseExtensions
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initializer = scope.ServiceProvider.GetRequiredService<Infrastructure.ApplicationDbContextInitializer>();

        var cancellationToken = app.Lifetime.ApplicationStopping;

        await initializer.InitializeAsync(cancellationToken);
        await initializer.SeedAsync(cancellationToken);
    }
}
