using Leontes.Application.CostControl;

namespace Leontes.Infrastructure.AI.CostControl;

internal sealed class CostDashboard(ITokenLedger ledger) : ICostDashboard
{
    public Task<DailyBudgetReport> GetTodayAsync(CancellationToken cancellationToken) =>
        ledger.GetTodayAsync(cancellationToken);

    public Task<IReadOnlyList<DailyBudgetReport>> GetHistoryAsync(int days, CancellationToken cancellationToken) =>
        ledger.GetHistoryAsync(days, cancellationToken);
}
