namespace Leontes.Application.ThinkingPipeline;

public interface ITokenMeter
{
    void Record(string stage, int inputTokens, int outputTokens);
}
