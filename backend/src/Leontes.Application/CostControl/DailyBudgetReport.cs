namespace Leontes.Application.CostControl;

public sealed record DailyBudgetReport(
    DateTime Date,
    int TotalTokensUsed,
    int DailyBudget,
    double PercentUsed,
    IReadOnlyDictionary<string, FeatureUsageReport> ByFeature,
    decimal? EstimatedCostUsd);

public sealed record FeatureUsageReport(
    string Feature,
    int InputTokens,
    int OutputTokens,
    int CallCount,
    string PrimaryModel);
