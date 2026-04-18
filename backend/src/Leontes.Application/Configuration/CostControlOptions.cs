namespace Leontes.Application.Configuration;

public sealed class CostControlOptions
{
    public const string SectionName = "CostControl";

    public int DailyTokenBudget { get; set; } = 500_000;
    public int WarningThresholdPercent { get; set; } = 75;
    public int ThrottleThresholdPercent { get; set; } = 90;
    public bool HardStopEnabled { get; set; }
    public int HardStopThresholdPercent { get; set; } = 100;
    public int BackgroundThrottleDelaySeconds { get; set; } = 30;
    public int UsageRetentionDays { get; set; } = 90;

    public Dictionary<string, int> FeatureAllocations { get; set; } = new()
    {
        ["Chat"] = 60,
        ["Sentinel"] = 15,
        ["Consolidation"] = 15,
        ["ToolForge"] = 10
    };

    public Dictionary<string, ModelCostOptions> ModelCosts { get; set; } = new();
}

public sealed class ModelCostOptions
{
    public decimal InputTokenCost { get; set; }
    public decimal OutputTokenCost { get; set; }
}
