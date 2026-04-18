using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Application.ThinkingPipeline;

public interface IContextWindowManager
{
    /// <summary>
    /// Returns a trimmed copy of the context with enrichment, history, and
    /// memories shrunk until the estimated token count fits the model's window.
    /// </summary>
    Task<ThinkingContext> FitAsync(
        ThinkingContext context,
        int modelTokenLimit,
        CancellationToken cancellationToken);
}
