using Leontes.Application.ThinkingPipeline;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

internal sealed class NullTokenMeter : ITokenMeter
{
    public void Record(string stage, int inputTokens, int outputTokens)
    {
        // No-op until feature 100 (Token Budget) is implemented
    }
}
