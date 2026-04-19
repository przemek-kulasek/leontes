namespace Leontes.Application.Vision;

public sealed class VisionOptions
{
    public const string SectionName = "Vision";

    public bool Enabled { get; set; }
    public int MaxTreeDepth { get; set; } = 8;
    public int MaxTokenEstimate { get; set; } = 4000;
    public bool IncludeBounds { get; set; }
    public bool RequireExplicitRequest { get; set; } = true;
    public bool ExcludePasswordFields { get; set; } = true;

    /// <summary>
    /// Process names (case-insensitive substring match) whose windows are never captured.
    /// Defaults live in appsettings.json; an empty list here means the config binder decides.
    /// </summary>
    public IList<string> ExcludedProcesses { get; set; } = [];
}
