namespace Leontes.Application.CostControl;

public static class CostControlFeatures
{
    public const string Chat = "Chat";
    public const string Sentinel = "Sentinel";
    public const string Consolidation = "Consolidation";
    public const string ToolForge = "ToolForge";
    public const string Other = "Other";

    public static bool IsInteractive(string feature) => feature == Chat;
}
