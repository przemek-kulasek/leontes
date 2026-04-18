using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Leontes.Application.Sentinel;
using Leontes.Infrastructure.Sentinel;
using Microsoft.Extensions.Options;

namespace Leontes.Worker.Sentinel;

[SupportedOSPlatform("windows")]
public sealed class ClipboardMonitor(
    ILogger<ClipboardMonitor> logger,
    IOptions<SentinelOptions> options,
    ISentinelHeuristicEngine engine,
    ISentinelEventQueue queue) : BackgroundService, IClipboardMonitor
{
    private readonly SentinelOptions _options = options.Value;
    private string? _lastContentHash;

    Task IClipboardMonitor.StartAsync(CancellationToken cancellationToken) =>
        StartAsync(cancellationToken);

    Task IClipboardMonitor.StopAsync(CancellationToken cancellationToken) =>
        StopAsync(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.Monitors.Clipboard.Enabled)
        {
            logger.LogInformation("ClipboardMonitor disabled via configuration");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            logger.LogWarning("ClipboardMonitor is only supported on Windows — monitor will not start");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.Monitors.Clipboard.PollIntervalSeconds));
        logger.LogInformation("ClipboardMonitor polling every {Interval}s", interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                PollClipboard();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ClipboardMonitor poll failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void PollClipboard()
    {
        if (!TryReadClipboardText(out var text) || string.IsNullOrEmpty(text))
            return;

        var hash = Hash(text);
        if (hash == _lastContentHash)
            return;

        _lastContentHash = hash;

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["contentHash"] = hash,
            ["length"] = text.Length.ToString()
        };

        var sentinelEvent = engine.Process(ClipboardContentFilter.Source, text, metadata);
        if (sentinelEvent is null)
            return;

        _ = queue.TryEnqueueAsync(sentinelEvent, CancellationToken.None);
    }

    private static string Hash(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private const uint CF_UNICODETEXT = 13;

    private static bool TryReadClipboardText(out string text)
    {
        text = string.Empty;
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
            return false;

        if (!OpenClipboard(IntPtr.Zero))
            return false;

        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
                return false;

            var locked = GlobalLock(handle);
            if (locked == IntPtr.Zero)
                return false;

            try
            {
                text = Marshal.PtrToStringUni(locked) ?? string.Empty;
                return true;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}
