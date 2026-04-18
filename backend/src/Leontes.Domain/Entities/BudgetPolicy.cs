namespace Leontes.Domain.Entities;

public sealed class BudgetPolicy : Entity
{
    public int DailyTokenBudget { get; set; } = 500_000;
    public int WarningThresholdPercent { get; set; } = 75;
    public int ThrottleThresholdPercent { get; set; } = 90;
    public bool HardStopEnabled { get; set; }
    public int HardStopThresholdPercent { get; set; } = 100;
    public Dictionary<string, int> FeatureAllocations { get; set; } = new()
    {
        ["Chat"] = 60,
        ["Sentinel"] = 15,
        ["Consolidation"] = 15,
        ["ToolForge"] = 10
    };
}
