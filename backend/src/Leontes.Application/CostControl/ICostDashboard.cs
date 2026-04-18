namespace Leontes.Application.CostControl;

public interface ICostDashboard
{
    Task<DailyBudgetReport> GetTodayAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyBudgetReport>> GetHistoryAsync(int days, CancellationToken cancellationToken);
}
