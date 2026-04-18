using Leontes.Application.CostControl;
using Leontes.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI.CostControl;

internal sealed class ModelRouter(
    ITokenLedger ledger,
    ILogger<ModelRouter> logger) : IModelRouter
{
    public async Task<ModelSelection> SelectModelAsync(
        ModelRoutingContext context,
        CancellationToken cancellationToken)
    {
        BudgetState state;
        try
        {
            var status = await ledger.GetGlobalBudgetStatusAsync(cancellationToken);
            state = status.State;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Budget status query failed in ModelRouter; using preferred tier");
            return Select(context.PreferredTier, BudgetState.Normal, downgraded: false, "Budget status unavailable");
        }

        return state switch
        {
            BudgetState.Normal => Select(context.PreferredTier, state, false, "Budget normal"),

            BudgetState.Warning when !CostControlFeatures.IsInteractive(context.Feature) =>
                Select(ModelTier.Small, state, context.PreferredTier == ModelTier.Large,
                    "Background task downgraded to Small tier (Warning)"),

            BudgetState.Warning =>
                Select(context.PreferredTier, state, false, "Interactive task preserved at Warning"),

            BudgetState.Throttled =>
                Select(ModelTier.Small, state, context.PreferredTier == ModelTier.Large,
                    "All tasks downgraded to Small tier (Throttled)"),

            BudgetState.Exhausted =>
                Select(ModelTier.Small, state, context.PreferredTier == ModelTier.Large,
                    "Budget exhausted — using smallest model"),

            _ => Select(context.PreferredTier, state, false, "Default")
        };
    }

    private static ModelSelection Select(ModelTier tier, BudgetState state, bool downgraded, string reason)
    {
        var key = tier == ModelTier.Large ? "Large" : "Small";
        return new ModelSelection(key, tier, state, downgraded, reason);
    }
}
