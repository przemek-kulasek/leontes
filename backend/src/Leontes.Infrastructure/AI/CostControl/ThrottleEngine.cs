using Leontes.Application.Configuration;
using Leontes.Application.CostControl;
using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.CostControl;

internal sealed class ThrottleEngine(
    ITokenLedger ledger,
    IWorkflowEventBridge eventBridge,
    BudgetWarningThrottler notifyThrottler,
    IOptions<CostControlOptions> options,
    ILogger<ThrottleEngine> logger) : IThrottleEngine
{
    public async Task<ThrottleDecision> EvaluateAsync(
        string feature,
        string operation,
        CancellationToken cancellationToken)
    {
        TokenBudgetStatus status;
        try
        {
            status = await ledger.GetGlobalBudgetStatusAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Budget status query failed; failing open for {Feature}/{Operation}", feature, operation);
            return new ThrottleDecision(true, null, null, null);
        }

        await NotifyIfThresholdCrossedAsync(status, cancellationToken);

        var interactive = CostControlFeatures.IsInteractive(feature);

        return status.State switch
        {
            BudgetState.Normal => new ThrottleDecision(true, null, null, null),

            BudgetState.Warning when interactive =>
                new ThrottleDecision(true, null, null,
                    $"Token budget at {status.PercentUsed:F0}% — consider pausing background tasks."),

            BudgetState.Warning =>
                new ThrottleDecision(true,
                    TimeSpan.FromSeconds(options.Value.BackgroundThrottleDelaySeconds), null, null),

            BudgetState.Throttled when interactive =>
                new ThrottleDecision(true, null, null,
                    $"Token budget at {status.PercentUsed:F0}% — using smaller model to conserve tokens."),

            BudgetState.Throttled =>
                new ThrottleDecision(false, null,
                    $"Background task '{feature}' deferred: budget at {status.PercentUsed:F0}%.", null),

            BudgetState.Exhausted when interactive && !IsHardStopActive(status) =>
                new ThrottleDecision(true, null, null,
                    $"Token budget exceeded ({status.PercentUsed:F0}%) — responses may be degraded."),

            BudgetState.Exhausted =>
                new ThrottleDecision(false, null,
                    $"Token budget exhausted ({status.PercentUsed:F0}%). Increase the daily budget or wait for the window to roll over.",
                    null),

            _ => new ThrottleDecision(true, null, null, null)
        };
    }

    private bool IsHardStopActive(TokenBudgetStatus status)
    {
        return options.Value.HardStopEnabled
            && status.PercentUsed >= options.Value.HardStopThresholdPercent;
    }

    private async Task NotifyIfThresholdCrossedAsync(TokenBudgetStatus status, CancellationToken cancellationToken)
    {
        if (!notifyThrottler.ShouldNotify(status.State))
            return;

        try
        {
            DailyBudgetReport today = await ledger.GetTodayAsync(cancellationToken);
            var topFeature = today.ByFeature.Count == 0
                ? "-"
                : today.ByFeature.OrderByDescending(kv => kv.Value.InputTokens + kv.Value.OutputTokens).First().Key;

            await eventBridge.PublishEventAsync(
                new BudgetWarningEvent(status.State, status.TokensUsed, status.TokenBudget, status.PercentUsed, topFeature),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish budget warning event");
        }
    }
}
