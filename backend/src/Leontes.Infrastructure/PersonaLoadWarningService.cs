using Leontes.Application.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure;

internal sealed class PersonaLoadWarningService(
    PersonaInstructions persona,
    ILogger<PersonaLoadWarningService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (persona.IsFromFallback)
        {
            logger.LogWarning(
                "Persona file not found at {PersonaPath}. Using fallback instructions; agent voice will be generic. " +
                "Verify Persona:InstructionsFile configuration and that persona.md is copied to the output directory.",
                persona.AttemptedPath);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
