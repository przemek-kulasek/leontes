using Leontes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure;

public sealed class ApplicationDbContextInitializer(
    ApplicationDbContext context,
    ILogger<ApplicationDbContextInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying database migrations");

        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database migrations applied");
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Checking for seed data");

        await SeedDefaultDataAsync(cancellationToken);
    }

    private Task SeedDefaultDataAsync(CancellationToken cancellationToken)
    {
        // Seed default data here as entities are added
        return Task.CompletedTask;
    }
}
