using Leontes.Domain.Enums;
using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication.Events;

public sealed class BudgetWarningEvent(
    BudgetState state,
    int tokensUsed,
    int tokenBudget,
    double percentUsed,
    string topFeature) : WorkflowEvent(new BudgetWarningPayload(state, tokensUsed, tokenBudget, percentUsed, topFeature));

public sealed record BudgetWarningPayload(
    BudgetState State,
    int TokensUsed,
    int TokenBudget,
    double PercentUsed,
    string TopFeature);
