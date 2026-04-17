using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

/// <summary>
/// Executes a stage action inside a recovery boundary. On failure, invokes the
/// degraded fallback rather than propagating, so the pipeline continues with
/// reduced capability (feature 85).
/// </summary>
internal static class StageRecovery
{
    public static async ValueTask<TOutput> RunAsync<TOutput>(
        string stageName,
        Func<CancellationToken, ValueTask<TOutput>> action,
        Func<Exception, TOutput> degradedFallback,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Stage {StageName} failed; falling back to degraded mode",
                stageName);
            return degradedFallback(ex);
        }
    }
}
