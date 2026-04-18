using System.Collections.Concurrent;
using Leontes.Application.Sentinel;
using Leontes.Infrastructure.Sentinel;
using Microsoft.Extensions.Options;

namespace Leontes.Worker.Sentinel;

public sealed class FileSystemMonitor(
    ILogger<FileSystemMonitor> logger,
    IOptions<SentinelOptions> options,
    ISentinelHeuristicEngine engine,
    ISentinelEventQueue queue) : BackgroundService, IFileSystemWatcher
{
    private readonly List<System.IO.FileSystemWatcher> _watchers = [];
    private readonly ConcurrentQueue<DateTime> _recentEvents = new();
    private readonly SentinelOptions _options = options.Value;

    Task IFileSystemWatcher.StartAsync(CancellationToken cancellationToken) =>
        StartAsync(cancellationToken);

    Task IFileSystemWatcher.StopAsync(CancellationToken cancellationToken) =>
        StopAsync(cancellationToken);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.Monitors.FileSystem.Enabled)
        {
            logger.LogInformation("FileSystemMonitor disabled via configuration");
            return Task.CompletedTask;
        }

        var watchPaths = _options.Monitors.FileSystem.WatchPaths
            .Select(ExpandPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (watchPaths.Count == 0)
        {
            logger.LogWarning("FileSystemMonitor has no valid watch paths configured — monitor will not start");
            return Task.CompletedTask;
        }

        foreach (var path in watchPaths)
        {
            try
            {
                var watcher = CreateWatcher(path);
                _watchers.Add(watcher);
                logger.LogInformation("FileSystemMonitor watching {Path}", path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start FileSystemWatcher for {Path}", path);
            }
        }

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        await base.StopAsync(cancellationToken);
    }

    private System.IO.FileSystemWatcher CreateWatcher(string path)
    {
        var watcher = new System.IO.FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            InternalBufferSize = 64 * 1024
        };

        watcher.Created += (_, e) => HandleEvent(e.FullPath, "Created");
        watcher.Changed += (_, e) => HandleEvent(e.FullPath, "Changed");
        watcher.Deleted += (_, e) => HandleEvent(e.FullPath, "Deleted");
        watcher.Renamed += (_, e) => HandleEvent(e.FullPath, "Renamed");
        watcher.Error += (_, e) => logger.LogError(e.GetException(), "FileSystemWatcher error on {Path}", path);

        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void HandleEvent(string fullPath, string changeType)
    {
        try
        {
            if (ShouldExclude(fullPath))
                return;

            RecordEvent();

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FileSystemEventFilter.MetaChangeType] = changeType,
                [FileSystemEventFilter.MetaExtension] = Path.GetExtension(fullPath),
                [FileSystemEventFilter.MetaRecentEventCount] = CountRecentEvents().ToString()
            };

            if (changeType is "Created" or "Changed" && File.Exists(fullPath))
            {
                try
                {
                    var size = new FileInfo(fullPath).Length;
                    metadata[FileSystemEventFilter.MetaSizeBytes] = size.ToString();
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            var sentinelEvent = engine.Process(FileSystemEventFilter.Source, fullPath, metadata);
            if (sentinelEvent is null)
                return;

            _ = queue.TryEnqueueAsync(sentinelEvent, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FileSystemMonitor failed to handle event for {Path}", fullPath);
        }
    }

    private void RecordEvent()
    {
        var now = DateTime.UtcNow;
        _recentEvents.Enqueue(now);
        TrimRecentEvents(now);
    }

    private int CountRecentEvents()
    {
        TrimRecentEvents(DateTime.UtcNow);
        return _recentEvents.Count;
    }

    private void TrimRecentEvents(DateTime now)
    {
        var cutoff = now - TimeSpan.FromSeconds(_options.Monitors.FileSystem.RapidChangeWindowSeconds);
        while (_recentEvents.TryPeek(out var head) && head < cutoff)
            _recentEvents.TryDequeue(out _);
    }

    private bool ShouldExclude(string fullPath)
    {
        var excludes = _options.Monitors.FileSystem.ExcludePatterns;
        if (excludes.Count == 0)
            return false;

        var fileName = Path.GetFileName(fullPath);
        foreach (var pattern in excludes)
        {
            if (MatchesGlob(fileName, pattern) || fullPath.Contains(TrimGlob(pattern), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool MatchesGlob(string fileName, string pattern)
    {
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
            return fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimGlob(string pattern)
    {
        var trimmed = pattern.Replace("**", "", StringComparison.Ordinal).Replace("*", "", StringComparison.Ordinal);
        return trimmed.Trim('/', '\\');
    }

    private static string ExpandPath(string path) =>
        Environment.ExpandEnvironmentVariables(path.Replace("{user}", Environment.UserName, StringComparison.Ordinal));
}
