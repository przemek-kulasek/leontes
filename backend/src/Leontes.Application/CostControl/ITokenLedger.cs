namespace Leontes.Application.CostControl;

public interface ITokenLedger
{
    Task RecordUsageAsync(TokenUsage usage, CancellationToken cancellationToken);

    Task<TokenBudgetStatus> GetGlobalBudgetStatusAsync(CancellationToken cancellationToken);

    Task<TokenBudgetStatus> GetFeatureBudgetStatusAsync(string feature, CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyBudgetReport>> GetHistoryAsync(int days, CancellationToken cancellationToken);

    Task<DailyBudgetReport> GetTodayAsync(CancellationToken cancellationToken);
}
