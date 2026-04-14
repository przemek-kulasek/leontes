# 80 — Sentinel Intelligence

## Problem

The Sentinel service exists as a stub — interfaces are defined (`IFileSystemWatcher`, `IClipboardMonitor`, `ICalendarMonitor`, `IActiveWindowMonitor`) but nothing is wired. More importantly, the current design sends every OS event to the LLM, which is expensive and slow. The Sentinel needs a fast heuristic layer (System 1) that filters, classifies, and prioritizes events before escalating only genuinely surprising or actionable events to the LLM (System 2).

## Prerequisites

- Working API with Processing Loop (feature 10)
- Thinking Pipeline (feature 65) — Sentinel events feed into the pipeline's Perceive stage
- Signal support (feature 50) — notifications can be delivered via Signal

## Rules

- No new NuGet packages required — `System.IO.FileSystemWatcher` and Windows APIs are built into .NET
- Sentinel runs in Leontes.Worker only (needs OS-level access)
- Heuristic filters run locally — no network calls, no LLM calls at the System 1 layer
- LLM escalation (System 2) goes through the API via HTTP, same as CLI/Signal messages
- Each monitor implements its Application-layer interface
- Event rate limiting: no more than 1 LLM escalation per monitor per minute (configurable)
- All monitors must be independently enable/disable via configuration
- Monitors must not crash the Worker if they encounter errors — log and continue

## Background

### System 1 vs System 2 (Kahneman)

**System 1 (this feature):** Fast, automatic, pattern-matching. The Sentinel monitors OS events and applies local heuristics — regex, file extension checks, time-based rules, frequency analysis. No LLM involved. Costs nothing.

**System 2 (Thinking Pipeline):** Slow, deliberate, expensive. Only triggered when System 1 detects something "surprising" — an event that doesn't match known patterns and might need intelligent interpretation.

**Example flow:**
1. User copies text to clipboard → System 1 checks: is it an IBAN? An email? A URL?
2. If IBAN detected → System 1 fires a structured event: `{ type: "clipboard", pattern: "iban", value: "DE89..." }`
3. Event sent to API → Thinking Pipeline → LLM: "User copied a bank account number. Should I find the last invoice or payment?"
4. If no pattern matches → event is logged and discarded. No LLM call.

### Free Energy Principle (Friston)

The brain acts to minimize surprise (prediction error). The Sentinel should track baseline patterns and only alert when something deviates:
- User always opens VS Code at 9am → not surprising, no alert
- User opens VS Code at 2am → surprising, maybe offer "late night? want me to set a reminder for tomorrow?"

This is implemented via a simple frequency/time model, not a full Bayesian framework.

### OpenClaw Lane Queue Principle

Events from different monitors go into the same processing queue but are handled serially to prevent state conflicts. A file system event and a clipboard event arriving simultaneously are queued and processed one at a time through the Thinking Pipeline.

## Solution

### Architecture Overview

```
OS Events (file system, clipboard, calendar, active window)
    |
    v
[Monitor Implementations] — IFileSystemWatcher, IClipboardMonitor, etc.
    |  raw events
    v
[Heuristic Filter] — System 1: regex, patterns, frequency, time rules
    |  filtered + classified events
    v
[Rate Limiter] — max 1 escalation/monitor/minute
    |  rate-limited events
    v
[Event Queue] — bounded in-memory channel (feature 75)
    |
    v
[SentinelService] — dequeues and forwards to API
    |  HTTP POST to /api/v1/messages (channel: "Sentinel")
    v
Leontes.Api → Thinking Pipeline → Response
    |
    v
[Notification] — CLI toast or Signal message
```

### Components

#### 1. SentinelEvent (Application Layer)

```csharp
public sealed record SentinelEvent(
    string MonitorSource,
    string EventType,
    string? Pattern,
    string Summary,
    Dictionary<string, string> Metadata,
    DateTime OccurredAt,
    SentinelPriority Priority);

public enum SentinelPriority
{
    Low,
    Medium,
    High,
    Critical
}
```

#### 2. ISentinelFilter (Application Layer)

```csharp
public interface ISentinelFilter
{
    string MonitorSource { get; }
    SentinelEvent? Evaluate(string rawEvent, Dictionary<string, string> metadata);
}
```

Each filter takes raw monitor output and either returns a classified `SentinelEvent` (worth escalating) or `null` (discard). This is the System 1 heuristic layer.

#### 3. Monitor Implementations (Infrastructure Layer)

**FileSystemMonitor**

Wraps `System.IO.FileSystemWatcher` to monitor configured directories:

```csharp
public sealed class FileSystemMonitor : IFileSystemWatcher, IDisposable
{
    private readonly System.IO.FileSystemWatcher _watcher;

    // Monitors: Created, Changed, Deleted, Renamed events
    // Filters: skip temp files, build artifacts, .git folders
    // Reports: file path, change type, extension, size
}
```

Heuristic filters for file system:
- Large file created (>100MB) → alert
- Known sensitive file extension (`.env`, `.pem`, `.key`) created outside expected locations → alert
- Rapid file changes in watched directory (>50 in 10 seconds) → alert (possible build/deploy)
- New file in Downloads folder matching invoice/receipt patterns → alert

**ClipboardMonitor**

Polls clipboard content on interval (Windows API via P/Invoke or `System.Windows.Forms.Clipboard`):

```csharp
public sealed class ClipboardMonitor : IClipboardMonitor
{
    // Polls every 2 seconds
    // Detects: text changes only (not images/files for now)
    // Reports: content hash (not full content — privacy), content type
}
```

Heuristic filters for clipboard:
- IBAN pattern (`[A-Z]{2}\d{2}[A-Z0-9]{4,}`) → "Bank account detected"
- Email address → "Email address copied"
- URL → "URL copied"
- JSON/XML/code block → "Structured data copied"
- Password-like string (high entropy, 12+ chars) → "Possible credential copied" (log warning, don't store the value)

**CalendarMonitor**

Reads Windows calendar via COM interop or Outlook REST API:

```csharp
public sealed class CalendarMonitor : ICalendarMonitor
{
    // Polls every 5 minutes
    // Reports: upcoming events in next 15 minutes
    // Filters: skip all-day events, recurring daily standup (frequency filter)
}
```

Heuristic filters:
- Meeting in 10 minutes with specific keywords ("review", "demo", "deadline") → alert
- Double-booked time slot → alert
- Meeting with external attendees → alert with attendee context from Synapse Graph

**ActiveWindowMonitor**

Tracks foreground window changes via Windows UI Automation:

```csharp
public sealed class ActiveWindowMonitor : IActiveWindowMonitor
{
    // Listens to focus change events via UI Automation
    // Reports: window title, process name, timestamp
    // Filters: debounce rapid switches (500ms), ignore system windows
}
```

Heuristic filters:
- Context switch frequency >10 per minute → "Frequent task switching detected"
- Specific application opened (e.g., banking app, password manager) → contextual awareness
- Time spent in single application >2 hours → "Deep focus session" (suppress non-critical alerts)

#### 4. SentinelHeuristicEngine (Infrastructure Layer)

Orchestrates all filters:

```csharp
public sealed class SentinelHeuristicEngine(
    IEnumerable<ISentinelFilter> filters,
    ILogger<SentinelHeuristicEngine> logger)
{
    public SentinelEvent? Process(string monitorSource, string rawEvent, Dictionary<string, string> metadata)
    {
        var filter = filters.FirstOrDefault(f => f.MonitorSource == monitorSource);
        if (filter is null) return null;

        return filter.Evaluate(rawEvent, metadata);
    }
}
```

#### 5. SentinelService (Worker Layer — existing, to be expanded)

```csharp
public sealed class SentinelService(
    IEnumerable<IHostedService> monitors,
    SentinelHeuristicEngine engine,
    Channel<SentinelEvent> eventChannel,
    HttpClient apiClient,
    ILogger<SentinelService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var sentinelEvent in eventChannel.Reader.ReadAllAsync(stoppingToken))
        {
            // Rate limit check
            // Forward to API as a message with channel: "Sentinel"
            // Deliver response as notification (CLI toast or Signal)
        }
    }
}
```

### Configuration

```json
{
    "Sentinel": {
        "Enabled": true,
        "RateLimitPerMonitorPerMinute": 1,
        "Monitors": {
            "FileSystem": {
                "Enabled": true,
                "WatchPaths": ["C:\\Users\\{user}\\Downloads", "C:\\Users\\{user}\\Documents"],
                "ExcludePatterns": ["*.tmp", "*.log", ".git/**"]
            },
            "Clipboard": {
                "Enabled": true,
                "PollIntervalSeconds": 2
            },
            "Calendar": {
                "Enabled": false,
                "PollIntervalMinutes": 5,
                "AlertMinutesBefore": 10
            },
            "ActiveWindow": {
                "Enabled": false,
                "DebounceMs": 500
            }
        }
    }
}
```

Monitors default to disabled where they require additional setup (Calendar needs Outlook access, ActiveWindow needs UI Automation permissions). FileSystem and Clipboard are enabled by default as they require no additional configuration.

### Notification Delivery (via Proactive Communication — Feature 55)

Sentinel notifications and alerts use the Proactive Communication infrastructure (feature 55). The SentinelService emits events through the `IWorkflowEventBridge` rather than managing channel selection directly:

```csharp
// In SentinelService — after heuristic filter matches
await eventBridge.PublishEventAsync(
    new NotificationEvent(
        sentinelEvent.Summary,
        FormatSentinelContext(sentinelEvent),
        MapPriority(sentinelEvent.Priority)),
    cancellationToken);
```

For Sentinel events that need user input (e.g., "You copied an IBAN — want me to find the last invoice?"), the Sentinel escalates to the Thinking Pipeline which uses a `RequestPort` (feature 55) to pause and wait for the user's response:

```csharp
// In the Thinking Pipeline, after Sentinel escalation
await context.SendMessageAsync(
    new SentinelAlert(
        MonitorSource: sentinelEvent.MonitorSource,
        Summary: sentinelEvent.Summary,
        SuggestedActions: ["Find invoice", "Dismiss"]));
// Workflow pauses — resumes when user responds or timeout expires
```

Channel selection (CLI SSE, Signal, Telegram, queued) is handled by the `IWorkflowEventBridge` — the Sentinel doesn't need to know which channel the user is on.

### Error Handling

| Scenario | Behavior |
|---|---|
| Monitor throws an exception | Log error, restart monitor after 30-second backoff |
| Heuristic filter throws | Log error, discard event, continue processing |
| Rate limit exceeded | Discard event silently, log at Debug level |
| API unreachable | Queue event for retry (max 3 attempts), then discard with Warning log |
| Event queue full (backpressure) | Drop event, log at Warning level — see feature 75 for queue depth limits |
| Monitor can't access OS resource (permissions) | Log error once, disable monitor, alert user on next CLI session |

### Security & Privacy

- Clipboard content is never stored in logs or database — only the pattern type ("IBAN detected", not the actual IBAN)
- File paths are logged but file contents are never read by monitors
- Active window titles may contain sensitive info — log process name only, not window title
- All Sentinel data is local — never transmitted to cloud services
- Password-like clipboard content triggers a warning but the value is immediately discarded

## Acceptance Criteria

- [ ] `IFileSystemWatcher` implementation monitors configured directories and detects file changes
- [ ] `IClipboardMonitor` implementation detects clipboard text changes and classifies content
- [ ] Heuristic filters correctly identify patterns (IBAN, email, URL, sensitive files) without LLM calls
- [ ] Events that pass heuristic filters are forwarded to the API via HTTP
- [ ] Rate limiting prevents more than N escalations per monitor per minute
- [ ] LLM receives structured context about the Sentinel event and produces an actionable response
- [ ] Responses are delivered as CLI notifications or Signal messages
- [ ] Each monitor can be independently enabled/disabled via configuration
- [ ] Monitor errors do not crash the Worker service
- [ ] Sensitive data (clipboard content, passwords) is never persisted or logged

## Out of Scope

- Calendar monitor implementation (requires Outlook COM interop or Microsoft Graph API — deferred)
- Active window monitor implementation (requires UI Automation setup — deferred, see feature 90)
- Machine learning–based anomaly detection (frequency model is heuristic, not learned)
- Cross-machine Sentinel (monitors local OS only)
- Custom user-defined heuristic rules (configuration is developer-managed for now)
- Voice/audio monitoring
- Screen content capture
