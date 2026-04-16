using Leontes.Application.ThinkingPipeline;

namespace Leontes.Infrastructure.AI.ThinkingPipeline;

internal sealed class NullDecisionRecorder : IDecisionRecorder
{
    public void Record(string stage, string decision, string rationale)
    {
        // No-op until feature 95 (Decision Records) is implemented
    }
}
