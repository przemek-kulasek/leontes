using Leontes.Application.CostControl;
using Leontes.Application.ThinkingPipeline;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI.CostControl;

internal sealed class TokenMeter(
    TokenUsageChannel channel,
    ILogger<TokenMeter> logger) : ITokenMeter
{
    public void Record(string feature, string operation, string modelId, int inputTokens, int outputTokens)
    {
        if (string.IsNullOrWhiteSpace(feature))
        {
            logger.LogWarning("Token usage recorded without a feature; classifying as {Fallback}", CostControlFeatures.Other);
            feature = CostControlFeatures.Other;
        }

        var usage = new TokenUsage(
            feature,
            operation,
            modelId,
            inputTokens,
            outputTokens,
            DateTime.UtcNow);

        if (!channel.Channel.Writer.TryWrite(usage))
        {
            logger.LogWarning(
                "Token usage channel full; dropped record for {Feature}/{Operation} ({Input}+{Output} tokens)",
                feature, operation, inputTokens, outputTokens);
        }
    }
}
