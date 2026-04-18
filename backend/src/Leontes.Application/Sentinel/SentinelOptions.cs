namespace Leontes.Application.Sentinel;

public sealed class SentinelOptions
{
    public const string SectionName = "Sentinel";

    public bool Enabled { get; set; } = true;

    public int RateLimitPerMonitorPerMinute { get; set; } = 1;

    public int QueueCapacity { get; set; } = 256;

    public SentinelMonitorOptions Monitors { get; set; } = new();
}

public sealed class SentinelMonitorOptions
{
    public FileSystemMonitorOptions FileSystem { get; set; } = new();

    public ClipboardMonitorOptions Clipboard { get; set; } = new();

    public CalendarMonitorOptions Calendar { get; set; } = new();

    public ActiveWindowMonitorOptions ActiveWindow { get; set; } = new();
}

public sealed class FileSystemMonitorOptions
{
    public bool Enabled { get; set; } = true;

    public List<string> WatchPaths { get; set; } = [];

    public List<string> ExcludePatterns { get; set; } = ["*.tmp", "*.log"];

    public long LargeFileThresholdBytes { get; set; } = 100L * 1024 * 1024;

    public int RapidChangeCount { get; set; } = 50;

    public int RapidChangeWindowSeconds { get; set; } = 10;
}

public sealed class ClipboardMonitorOptions
{
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 2;
}

public sealed class CalendarMonitorOptions
{
    public bool Enabled { get; set; }

    public int PollIntervalMinutes { get; set; } = 5;

    public int AlertMinutesBefore { get; set; } = 10;
}

public sealed class ActiveWindowMonitorOptions
{
    public bool Enabled { get; set; }

    public int DebounceMs { get; set; } = 500;
}
