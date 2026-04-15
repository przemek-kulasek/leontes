# 65 — Proactive Communication

## Problem

The assistant is currently reactive-only — it can respond to user messages but never initiate contact. The project vision defines a "Proactive OS Partner" that "acts before you ask," but the infrastructure to support this doesn't exist. There is no mechanism for the assistant to:

- Send unsolicited notifications (Sentinel alerts, task completions)
- Ask the user a question mid-task and pause until they respond
- Request permission before performing a sensitive action (Tool Forge approval, file modification)
- Stream progress updates during long-running operations
- Initiate a conversation about something it noticed or remembered

Without bidirectional proactive communication, the Sentinel is silent, the Tool Forge can't get approval, and the assistant is just another chatbot that waits.

## Prerequisites

- Working CLI chat with SSE streaming (feature 10)
- API key authentication (feature 40)
- At least one messaging channel operational (CLI or Signal/Telegram, feature 50)

## Rules

- Built on `Microsoft.Agents.AI.Workflows` — use `RequestPort` for human-in-the-loop, `WorkflowEvent` for progress, `CheckpointManager` for resumability
- No custom pub/sub or message bus — the Agent Framework's event stream is the single communication backbone
- Proactive messages must clearly indicate they are assistant-initiated (distinct from responses)
- The user must always be able to ignore, dismiss, or defer a proactive message
- Permission requests block the requesting operation until the user responds or a timeout expires
- The assistant must never take a sensitive action without explicit approval, even if the user doesn't respond (default: deny on timeout)
- All proactive events are persisted in the database with the same audit trail as user-initiated messages
- Channel selection is automatic: use the channel with an active connection, fall back to the next available

## New Package Required

`Microsoft.Agents.AI.Workflows` must be added to the approved package list and `Directory.Packages.props`. This is the workflow engine from the same Microsoft Agent Framework already approved (`Microsoft.Agents.AI`). It provides:

- `RequestPort<TRequest, TResponse>` — typed channels for external request/response (HITL)
- `WorkflowEvent` hierarchy — structured event stream for observability and progress
- `CheckpointManager` — state persistence across super steps for long-running workflows
- `StreamingRun` — real-time event consumption via `WatchStreamAsync()`
- `ApprovalRequiredAIFunction` — built-in tool approval wrapping
- `InProcessExecution` — workflow runner with checkpointing support

## Background

### Current Communication Model (Reactive)

```
User → POST /api/v1/messages → SSE stream → Response → Connection closes
```

Every interaction starts with the user. The SSE connection is per-request — it opens when the user sends a message and closes when the response is complete. The assistant has no way to reach the user between requests.

### Agent Framework Workflow Event Model

The Microsoft Agent Framework provides a complete event-driven architecture for agent communication. Instead of building custom infrastructure, Leontes leverages this directly:

```
WorkflowEvent (base)
├── ExecutorInvokedEvent        — stage started
├── ExecutorCompletedEvent      — stage finished
├── ExecutorFailedEvent         — stage failed
├── AgentResponseUpdateEvent    — streaming LLM token
├── SuperStepCompletedEvent     — checkpoint opportunity
├── RequestInfoEvent            — HITL: needs human input
├── RequestHaltEvent            — workflow paused
├── WorkflowOutputEvent         — final result
└── Custom events               — progress, notifications, insights
```

**Key insight:** `RequestInfoEvent` is the universal mechanism for proactive communication. When the workflow needs human input — a question, a permission, an approval — it emits a `RequestInfoEvent` with a typed payload. The host (CLI/Worker) catches it, presents it to the user, and sends the response back via `run.SendResponseAsync()`. The workflow resumes automatically.

This replaces custom `INotificationRouter`, `IProactiveEventChannel`, and `TaskCompletionSource` patterns with a single, framework-native mechanism that also supports checkpointing (pending requests survive restarts).

### Proactive Message Types (Mapped to Framework)

| Type | Framework Mechanism | Expects Response |
|---|---|---|
| Notification | Custom `WorkflowEvent` subclass | No |
| Status Update | Custom `WorkflowEvent` + `context.AddEventAsync()` | No |
| Question | `RequestPort<QuestionRequest, string>` → `RequestInfoEvent` | Yes |
| Permission Request | `ApprovalRequiredAIFunction` or `RequestPort<PermissionRequest, bool>` | Yes (approve/deny) |
| Alert | Custom `WorkflowEvent` with urgency | Optional |
| Tool Approval | `ApprovalRequiredAIFunction` → `ToolApprovalRequestContent` | Yes |

### Channel Priority & Fallback

The assistant must be able to reach the user through whatever channel is available:

1. **Active CLI session** (persistent SSE) — preferred, fastest
2. **Signal** (if configured) — E2E encrypted, mobile
3. **Telegram** (if configured) — mobile, easiest setup
4. **Queued for next session** — if no channel is active, store and deliver on next CLI connect

## Solution

### Architecture Overview

```
                         ┌─────────────────────────────┐
                         │   Leontes.Api                │
                         │                              │
   CLI ←── SSE ──────────┤  Workflow Engine              │
   CLI ──→ POST /respond │  (Microsoft.Agents.AI        │
                         │   .Workflows)                │
                         │       │                      │
                         │       ├── StreamingRun        │
                         │       │   .WatchStreamAsync() │
                         │       │                      │
                         │       ├── RequestInfoEvent    │←── Executor needs input
                         │       ├── Custom Events       │←── Progress, notifications
                         │       ├── CheckpointManager   │←── State persistence
                         │       └── WorkflowOutputEvent │←── Final results
                         │                              │
                         │   Event Bridge               │
                         │       │                      │
                         └───────┼──────────────────────┘
                                 │
                   ┌─────────────┼─────────────────┐
                   ▼             ▼                  ▼
             CLI (SSE)    Worker→Signal       Worker→Telegram
```

### Components

#### 1. Custom Workflow Events (Application Layer)

Domain-specific events extending `WorkflowEvent` for proactive communication:

```csharp
public sealed class NotificationEvent(
    string title,
    string content,
    ProactiveUrgency urgency) : WorkflowEvent(new NotificationPayload(title, content, urgency));

public sealed class ProgressEvent(
    string stage,
    string description,
    double? progress) : WorkflowEvent(new ProgressPayload(stage, description, progress));

public sealed class InsightEvent(
    string content,
    string source) : WorkflowEvent(new InsightPayload(content, source));

public sealed record NotificationPayload(string Title, string Content, ProactiveUrgency Urgency);
public sealed record ProgressPayload(string Stage, string Description, double? Progress);
public sealed record InsightPayload(string Content, string Source);

public enum ProactiveUrgency
{
    Low,
    Medium,
    High,
    Critical
}
```

Emitted from any executor via `context.AddEventAsync()`:

```csharp
await context.AddEventAsync(
    new NotificationEvent("Meeting in 10 min", "Sarah - Q3 Review", ProactiveUrgency.Medium));
```

#### 2. RequestPorts for Human-in-the-Loop (Application Layer)

Typed communication channels for each interaction pattern:

```csharp
// Questions — executor asks, human answers with free text
public sealed record QuestionRequest(
    string Title,
    string Content,
    IReadOnlyList<string>? Options,
    TimeSpan? Timeout);

public static readonly RequestPort<QuestionRequest, string> QuestionPort =
    RequestPort.Create<QuestionRequest, string>("HumanQuestion");

// Permissions — executor asks, human approves or denies
public sealed record PermissionRequest(
    string Action,
    string Details,
    IReadOnlyList<string>? Context);

public static readonly RequestPort<PermissionRequest, bool> PermissionPort =
    RequestPort.Create<PermissionRequest, bool>("HumanPermission");

// Sentinel alerts — optional response (user can dismiss or act)
public sealed record SentinelAlert(
    string MonitorSource,
    string Summary,
    IReadOnlyList<string>? SuggestedActions);

public static readonly RequestPort<SentinelAlert, string?> SentinelAlertPort =
    RequestPort.Create<SentinelAlert, string?>("SentinelAlert");
```

#### 3. Persistent SSE Event Bridge (Api Layer)

A single persistent SSE endpoint that bridges `WorkflowEvent` streams to the CLI:

```csharp
// GET /api/v1/stream — persistent SSE channel bridging workflow events
app.MapGet("/api/v1/stream", async (
    IWorkflowEventBridge bridge,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    var clientId = httpContext.Connection.Id;
    bridge.RegisterClient(clientId);

    try
    {
        await foreach (var evt in bridge.ReadEventsAsync(clientId, cancellationToken))
        {
            var sseEvent = evt switch
            {
                RequestInfoEvent req => FormatRequestEvent(req),
                NotificationEvent n => FormatNotification(n),
                ProgressEvent p => FormatProgress(p),
                AgentResponseUpdateEvent u => FormatStreamingToken(u),
                SuperStepCompletedEvent s => FormatCheckpoint(s),
                _ => null
            };

            if (sseEvent is not null)
            {
                await httpContext.Response.WriteAsync(sseEvent, cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
    finally
    {
        bridge.UnregisterClient(clientId);
    }
}).RequireAuthorization();
```

**SSE Event Types on the persistent channel:**

```
event: request
data: {"requestId": "abc-123", "type": "question", "title": "Tool Forge", "content": "Approve this tool?", "options": ["Approve", "Reject", "Show Code"], "timeoutSeconds": 300}

event: request
data: {"requestId": "def-456", "type": "toolApproval", "toolName": "csv-to-json", "toolCall": "..."}

event: notification
data: {"title": "Meeting in 10 min", "content": "Sarah - Q3 Review", "urgency": "Medium"}

event: progress
data: {"stage": "Enrich", "description": "Searching memory...", "progress": 0.4}

event: token
data: {"text": "Based on"}

event: checkpoint
data: {"stepNumber": 3, "checkpointId": "..."}

event: heartbeat
data: {}
```

#### 4. Response Endpoint (Api Layer)

When the user responds to a `RequestInfoEvent`:

```http
### Respond to a workflow request
POST /api/v1/stream/respond
Authorization: Bearer {token}
Content-Type: application/json

{
    "requestId": "abc-123",
    "response": "Approve"
}
```

The API routes the response back to the active `StreamingRun`:

```csharp
app.MapPost("/api/v1/stream/respond", async (
    RespondRequest request,
    IWorkflowSessionManager sessions,
    CancellationToken cancellationToken) =>
{
    var run = sessions.GetActiveRun();
    if (run is null)
        return Results.NotFound("No active workflow run");

    var pendingRequest = run.FindPendingRequest(request.RequestId);
    if (pendingRequest is null)
        return Results.NotFound("Request not found or already responded");

    await run.SendResponseAsync(
        pendingRequest.CreateResponse(request.Response));

    return Results.Ok();
}).RequireAuthorization();
```

The framework automatically routes the response back to the executor that sent the original request. No manual correlation needed — `RequestId` handles it.

#### 5. IWorkflowEventBridge (Application Layer)

Bridges workflow events to connected clients and messaging platforms:

```csharp
public interface IWorkflowEventBridge
{
    void RegisterClient(string clientId);
    void UnregisterClient(string clientId);
    bool HasActiveClients { get; }

    Task PublishEventAsync(WorkflowEvent evt, CancellationToken cancellationToken);
    IAsyncEnumerable<WorkflowEvent> ReadEventsAsync(
        string clientId,
        CancellationToken cancellationToken);
}
```

Implementation uses `System.Threading.Channels.Channel<WorkflowEvent>` per client. The workflow host pumps events from `StreamingRun.WatchStreamAsync()` into the bridge.

#### 6. IProactiveEventStore (Application Layer)

Persistence for events that can't be delivered immediately:

```csharp
public interface IProactiveEventStore
{
    Task StoreAsync(WorkflowEvent evt, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoredProactiveEvent>> GetPendingAsync(CancellationToken cancellationToken);
    Task MarkDeliveredAsync(Guid eventId, CancellationToken cancellationToken);
}
```

```csharp
public sealed class StoredProactiveEvent : Entity
{
    public required string EventType { get; set; }
    public required string PayloadJson { get; set; }
    public ProactiveUrgency Urgency { get; set; }
    public ProactiveEventStatus Status { get; set; }
    public string? RequestId { get; set; }
    public string? Response { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public enum ProactiveEventStatus
{
    Pending,
    Delivered,
    Responded,
    Expired,
    Dismissed
}
```

When a CLI session connects, pending events are flushed:

```csharp
// On client registration, deliver queued events
var pending = await eventStore.GetPendingAsync(cancellationToken);
foreach (var evt in pending)
{
    await bridge.PublishEventAsync(Deserialize(evt), cancellationToken);
    await eventStore.MarkDeliveredAsync(evt.Id, cancellationToken);
}
```

#### 7. Checkpointing for Resumable Proactive Workflows

When a workflow emits a `RequestInfoEvent` and the user doesn't respond before the server restarts, the checkpoint system preserves the pending request:

```csharp
// Workflow host in Leontes.Api
var checkpointManager = CheckpointManager.CreateJson(
    new PostgresCheckpointStore(dbContext));

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow, input, checkpointManager);

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case RequestInfoEvent req:
            // Persist for offline delivery
            await eventStore.StoreAsync(req, cancellationToken);
            // Push to active clients
            await bridge.PublishEventAsync(req, cancellationToken);
            break;

        case SuperStepCompletedEvent step when step.CompletionInfo?.Checkpoint is not null:
            // Checkpoint auto-saved — includes pending requests
            logger.LogDebug(
                "Checkpoint {CheckpointId} saved at step {StepNumber}",
                step.CompletionInfo.Checkpoint.CheckpointId,
                step.StepNumber);
            break;
    }
}
```

On restart, pending requests are re-emitted from the checkpoint:

```csharp
// Resume from last checkpoint after restart
var lastCheckpoint = await checkpointStore.GetLatestAsync(cancellationToken);
if (lastCheckpoint is not null)
{
    var run = await InProcessExecution.ResumeStreamingAsync(
        workflow, lastCheckpoint, checkpointManager);
    // Re-emitted RequestInfoEvents are caught and delivered to clients
}
```

### Data Model: Conversation & Message Changes

Add initiator tracking to support assistant-started conversations:

```csharp
// Conversation — new fields
public MessageInitiator InitiatedBy { get; set; } = MessageInitiator.User;
public bool IsProactive { get; set; }

// Message — new field
public MessageInitiator Initiator { get; set; } = MessageInitiator.User;

public enum MessageInitiator
{
    User,
    Assistant,
    Sentinel,
    System
}
```

### CLI Integration

The CLI maintains two connections:

1. **Persistent SSE** (`GET /api/v1/stream`) — open on startup, receives all workflow events
2. **Per-message POST** (`POST /api/v1/messages`) — sends user messages

```
┌─────────────────────────────────────────────────┐
│  leontes chat                                    │
│                                                  │
│  [Leontes] Meeting with Sarah in 10 minutes      │  ← NotificationEvent
│  [Leontes] She's the lead dev on Project Alpha   │
│                                                  │
│  You: What's on my agenda today?                 │  ← user message
│  [Leontes] Here's your schedule for today...     │  ← AgentResponseUpdateEvent stream
│                                                  │
│  ┌──────────────────────────────────────────┐    │
│  │ 🔧 Tool Forge: Approve new tool?         │    │  ← RequestInfoEvent (ToolApproval)
│  │                                          │    │
│  │ Name: csv-to-json                        │    │
│  │ Description: Converts CSV to JSON array  │    │
│  │                                          │    │
│  │ [A]pprove  [R]eject  [S]how Code        │    │
│  └──────────────────────────────────────────┘    │
│                                                  │
│  ▸ Enriching context... (step 2/5)               │  ← ProgressEvent
│                                                  │
│  You: _                                          │
└─────────────────────────────────────────────────┘
```

Proactive events render inline. `RequestInfoEvent` renders as interactive prompts. Responses go to `POST /api/v1/stream/respond` with the `requestId`.

### Worker Bridge Integration

Worker bridges forward proactive events to Signal/Telegram:

```csharp
// In SignalBridgeService — subscribe to workflow events
await foreach (var evt in bridge.ReadEventsAsync("signal-bridge", cancellationToken))
{
    switch (evt)
    {
        case RequestInfoEvent req:
            var formatted = FormatRequestForSignal(req);
            await messagingClient.SendMessageAsync(recipient, formatted, cancellationToken);
            // User replies with text → bridge maps to POST /api/v1/stream/respond
            break;

        case NotificationEvent notification:
            var payload = notification.As<NotificationPayload>();
            await messagingClient.SendMessageAsync(
                recipient, $"🔔 {payload.Title}\n{payload.Content}", cancellationToken);
            break;
    }
}
```

For questions via Signal/Telegram: user replies with option text ("Approve"), bridge maps it to `POST /api/v1/stream/respond`.

### How Executors Use Proactive Communication

Any executor in the Thinking Pipeline (feature 70) can use these patterns:

```csharp
// Emit progress (fire-and-forget, no response needed)
await context.AddEventAsync(
    new ProgressEvent("Enrich", "Searching memory for relevant context...", 0.3));

// Ask a question (pauses workflow until human responds)
await context.SendMessageAsync(
    new QuestionRequest(
        "Clarification needed",
        "Should I include archived projects?",
        Options: ["Yes, include archived", "No, active only"],
        Timeout: TimeSpan.FromMinutes(5)));
// Framework suspends executor, emits RequestInfoEvent
// When response arrives via SendResponseAsync(), executor resumes with the answer

// Request permission (via ApprovalRequiredAIFunction — automatic)
// Tools wrapped with ApprovalRequiredAIFunction emit ToolApprovalRequestContent
// in RequestInfoEvent automatically — no custom code needed
```

### Error Handling

| Scenario | Behavior |
|---|---|
| CLI disconnects during persistent SSE | Events queue in database, delivered on reconnect |
| No channel available | Events stored as Pending, delivered when any channel connects |
| Permission request times out | Default deny, log the timeout, notify user on next session |
| Question times out | Executor receives null/default, uses safe fallback |
| Server restarts with pending requests | Checkpoint restored, RequestInfoEvents re-emitted |
| Multiple CLI sessions connect | Events broadcast to all (deduplicated by requestId) |
| Worker bridge can't reach messaging platform | Retry with backoff, fall back to queue |

### Configuration

```json
{
    "ProactiveCommunication": {
        "DefaultQuestionTimeoutMinutes": 5,
        "DefaultPermissionTimeoutMinutes": 10,
        "HeartbeatIntervalSeconds": 30,
        "MaxPendingEvents": 100,
        "ChannelPriority": ["Cli", "Signal", "Telegram"],
        "CheckpointStorage": "PostgreSQL"
    }
}
```

## Acceptance Criteria

- [ ] Persistent SSE endpoint (`GET /api/v1/stream`) bridges `WorkflowEvent` streams to CLI
- [ ] `RequestInfoEvent` with `QuestionRequest` payload pauses workflow and waits for human response
- [ ] `RequestInfoEvent` with `PermissionRequest` payload blocks operation, defaults to deny on timeout
- [ ] `ApprovalRequiredAIFunction` wrapping emits `ToolApprovalRequestContent` automatically
- [ ] Custom `WorkflowEvent` subclasses (Notification, Progress, Insight) stream to CLI in real time
- [ ] `POST /api/v1/stream/respond` routes responses back to the correct `StreamingRun` via `requestId`
- [ ] `CheckpointManager` persists pending requests — they survive server restarts
- [ ] Worker bridges forward proactive events to Signal/Telegram when CLI is offline
- [ ] Events queued while no channel is active are delivered on next CLI connection
- [ ] Conversations and messages track who initiated them (User, Assistant, Sentinel, System)
- [ ] All proactive events are persisted in the database with full audit trail
- [ ] Heartbeat keeps persistent SSE connection alive

## Out of Scope

- WebSocket (SSE is sufficient for single-user, server→client push)
- AG-UI protocol integration (defer until a web dashboard is built — post-MVP)
- Push notifications to mobile OS (rely on Signal/Telegram app notifications)
- Email as a notification channel
- Scheduled/recurring notifications (Sentinel handles time-based triggers)
- User-configurable notification preferences (all events delivered, user can dismiss)
- Rich media in notifications (text only for now)
