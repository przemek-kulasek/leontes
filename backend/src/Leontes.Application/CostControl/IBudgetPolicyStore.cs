using Leontes.Domain.Entities;

namespace Leontes.Application.CostControl;

public interface IBudgetPolicyStore
{
    Task<BudgetPolicy> GetAsync(CancellationToken cancellationToken);

    Task UpdateAsync(BudgetPolicy policy, CancellationToken cancellationToken);
}
