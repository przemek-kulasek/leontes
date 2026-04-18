using Leontes.Domain.Enums;

namespace Leontes.Application.CostControl;

public sealed record TokenBudgetStatus(
    string Feature,
    int TokensUsed,
    int TokenBudget,
    double PercentUsed,
    BudgetState State);
