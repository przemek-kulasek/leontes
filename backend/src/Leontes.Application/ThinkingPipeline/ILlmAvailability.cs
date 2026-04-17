namespace Leontes.Application.ThinkingPipeline;

public interface ILlmAvailability
{
    bool IsAvailable { get; }
    DateTime? LastFailureAt { get; }
    int ConsecutiveFailures { get; }

    void RecordSuccess();
    void RecordFailure();
}
