namespace Leontes.Application.ThinkingPipeline;

public interface IDecisionRecorder
{
    void Record(string stage, string decision, string rationale);
}
