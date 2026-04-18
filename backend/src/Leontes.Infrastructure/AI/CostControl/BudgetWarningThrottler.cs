using Leontes.Domain.Enums;

namespace Leontes.Infrastructure.AI.CostControl;

internal sealed class BudgetWarningThrottler
{
    private static readonly TimeSpan NotifyInterval = TimeSpan.FromHours(1);
    private readonly Lock _gate = new();
    private DateTime _lastNotifiedUtc = DateTime.MinValue;
    private BudgetState _lastState = BudgetState.Normal;

    public bool ShouldNotify(BudgetState state)
    {
        if (state == BudgetState.Normal)
            return false;

        lock (_gate)
        {
            var now = DateTime.UtcNow;
            if (state == _lastState && now - _lastNotifiedUtc < NotifyInterval)
                return false;

            _lastState = state;
            _lastNotifiedUtc = now;
            return true;
        }
    }
}
