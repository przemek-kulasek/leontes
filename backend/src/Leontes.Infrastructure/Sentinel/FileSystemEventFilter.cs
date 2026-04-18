using Leontes.Application.Sentinel;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Sentinel;

public sealed class FileSystemEventFilter(IOptions<SentinelOptions> options) : ISentinelFilter
{
    public const string Source = "FileSystem";

    public const string MetaChangeType = "changeType";
    public const string MetaExtension = "extension";
    public const string MetaSizeBytes = "sizeBytes";
    public const string MetaFullPath = "fullPath";
    public const string MetaRecentEventCount = "recentEventCount";

    private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".pem", ".key", ".pfx", ".p12"
    };

    private static readonly string[] InvoicePatterns = ["invoice", "receipt", "rechnung", "bill"];

    private readonly SentinelOptions _options = options.Value;

    public string MonitorSource => Source;

    public SentinelEvent? Evaluate(string rawEvent, IReadOnlyDictionary<string, string> metadata)
    {
        var path = rawEvent;
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var changeType = metadata.TryGetValue(MetaChangeType, out var ct) ? ct : "Changed";
        var extension = metadata.TryGetValue(MetaExtension, out var ext) ? ext : Path.GetExtension(path);
        var fileName = Path.GetFileName(path);
        var size = ParseLong(metadata, MetaSizeBytes);
        var recentCount = ParseInt(metadata, MetaRecentEventCount);

        if (recentCount >= _options.Monitors.FileSystem.RapidChangeCount)
        {
            return Classify(path, "rapid-changes",
                $"Rapid file activity detected ({recentCount} events in {_options.Monitors.FileSystem.RapidChangeWindowSeconds}s)",
                SentinelPriority.Medium, changeType, extension, size);
        }

        if (!string.IsNullOrEmpty(extension) && SensitiveExtensions.Contains(extension))
        {
            return Classify(path, "sensitive-file",
                $"Sensitive file {changeType.ToLowerInvariant()}: {fileName}",
                SentinelPriority.High, changeType, extension, size);
        }

        if (size >= _options.Monitors.FileSystem.LargeFileThresholdBytes && IsCreatedOrChanged(changeType))
        {
            return Classify(path, "large-file",
                $"Large file ({FormatSize(size)}) {changeType.ToLowerInvariant()}: {fileName}",
                SentinelPriority.Low, changeType, extension, size);
        }

        if (LooksLikeInvoice(fileName) && IsCreatedOrChanged(changeType))
        {
            return Classify(path, "invoice",
                $"Invoice-like file {changeType.ToLowerInvariant()}: {fileName}",
                SentinelPriority.Medium, changeType, extension, size);
        }

        return null;
    }

    private static SentinelEvent Classify(
        string path,
        string pattern,
        string summary,
        SentinelPriority priority,
        string changeType,
        string? extension,
        long size)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MetaFullPath] = path,
            [MetaChangeType] = changeType
        };

        if (!string.IsNullOrEmpty(extension))
            metadata[MetaExtension] = extension;

        if (size > 0)
            metadata[MetaSizeBytes] = size.ToString();

        return new SentinelEvent(
            MonitorSource: Source,
            EventType: "filesystem",
            Pattern: pattern,
            Summary: summary,
            Metadata: metadata,
            OccurredAt: DateTime.UtcNow,
            Priority: priority);
    }

    private static bool LooksLikeInvoice(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        var lower = fileName.ToLowerInvariant();
        foreach (var pattern in InvoicePatterns)
        {
            if (lower.Contains(pattern))
                return true;
        }
        return false;
    }

    private static bool IsCreatedOrChanged(string changeType) =>
        changeType.Equals("Created", StringComparison.OrdinalIgnoreCase)
        || changeType.Equals("Changed", StringComparison.OrdinalIgnoreCase)
        || changeType.Equals("Renamed", StringComparison.OrdinalIgnoreCase);

    private static long ParseLong(IReadOnlyDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && long.TryParse(value, out var parsed) ? parsed : 0;

    private static int ParseInt(IReadOnlyDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : 0;

    private static string FormatSize(long bytes)
    {
        const double mb = 1024 * 1024;
        const double gb = mb * 1024;
        return bytes >= gb
            ? $"{bytes / gb:F1} GB"
            : $"{bytes / mb:F1} MB";
    }
}
