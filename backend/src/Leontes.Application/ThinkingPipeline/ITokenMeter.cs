namespace Leontes.Application.ThinkingPipeline;

public interface ITokenMeter
{
    void Record(string feature, string operation, string modelId, int inputTokens, int outputTokens);
}
