namespace Leontes.Application.CostControl;

public interface IThrottleEngine
{
    Task<ThrottleDecision> EvaluateAsync(
        string feature,
        string operation,
        CancellationToken cancellationToken);
}

public sealed record ThrottleDecision(
    bool Allowed,
    TimeSpan? DelayBefore,
    string? DenialReason,
    string? UserNotice);
