using Leontes.Domain.Enums;

namespace Leontes.Application.CostControl;

public interface IModelRouter
{
    Task<ModelSelection> SelectModelAsync(
        ModelRoutingContext context,
        CancellationToken cancellationToken);
}

public sealed record ModelRoutingContext(
    string Feature,
    string Operation,
    ModelTier PreferredTier);

public sealed record ModelSelection(
    string ModelKey,
    ModelTier Tier,
    BudgetState BudgetState,
    bool Downgraded,
    string Reason);

public enum ModelTier
{
    Small,
    Large
}
