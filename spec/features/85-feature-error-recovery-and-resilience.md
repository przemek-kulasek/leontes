# 85 — Error Recovery & Resilience

## Problem

Leontes is a multi-stage cognitive system where any layer can fail independently: the LLM provider times out, a tool crashes mid-execution, the database connection drops during memory consolidation, or Sentinel floods events faster than the pipeline can process. None of the existing specs define what happens when things go wrong at each layer.

Without a resilience strategy, a single transient failure can leave the agent stuck — a pending `RequestPort` with no timeout, a half-written memory entry, a Sentinel event queue growing unbounded. The user sees silence and has no idea whether the agent is thinking, broken, or dead. A world-class assistant must degrade gracefully, recover automatically where possible, and always communicate its state.

## Prerequisites

- Working feature 10 (CLI Chat)
- Working feature 65 (Proactive Communication — for failure notifications)
- Working feature 65 (Thinking Pipeline — the primary system to protect)

## Rules

- Every external call (LLM, database, HTTP) must have an explicit timeout — no unbounded waits
- Pipeline failures must never lose user input — the original message is always recoverable
- Degraded mode must still provide value — if the LLM is down, Sentinel heuristics and local tools still function
- No silent failures — every error the user should know about is surfaced via the proactive communication channel (feature 65)
- Backpressure is mandatory for all unbounded queues — define depth limits and overflow behavior
- Checkpoint-based recovery is the primary mechanism — leverage Agent Framework `CheckpointManager` from feature 65
- `AddStandardResilienceHandler()` is the baseline for HTTP clients — this spec defines the layer above that

## Background

### The Failure Taxonomy

Failures in a cognitive system fall into four categories, each requiring a different recovery strategy:

| Category | Examples | Recovery |
|---|---|---|
| **Transient** | LLM rate limit, network blip, DB connection pool exhausted | Retry with backoff |
| **Degraded** | LLM provider down, embedding service unavailable | Continue with reduced capability |
| **Partial** | Pipeline completes 3/5 stages, tool chain fails at step 2/4 | Checkpoint + resume or deliver partial result |
| **Fatal** | Database unreachable on startup, corrupt checkpoint | Fail loudly, notify user, preserve state |

### Backpressure and the Sentinel Problem

Feature 80 (Sentinel) monitors OS events continuously. Feature 65 (Thinking Pipeline) processes requests serially. If Sentinel produces events faster than the pipeline can consume them, three things can happen: unbounded memory growth (OOM), event loss (silent drop), or system lock-up. The answer is backpressure — a bounded queue with explicit overflow policy.

### The Timeout Hierarchy

Different operations have fundamentally different time expectations:

| Operation | Expected Latency | Timeout |
|---|---|---|
| Heuristic filter (System 1) | < 10ms | 100ms |
| Memory retrieval (pgvector) | < 500ms | 2s |
| Graph traversal (CTE) | < 200ms | 1s |
| LLM inference (local Ollama) | 2–30s | 60s |
| LLM inference (cloud) | 1–15s | 30s |
| Tool execution (forged) | < 5s | 15s |
| Tool execution (MCP external) | varies | 30s |
| SSE stream total | varies | 5min |
| RequestPort response (HITL) | user-dependent | configurable, default 10min |

### Context Window Overflow

Long conversations or memory-rich enrichments can exceed the LLM's context window. This is not an edge case — it is inevitable for a long-running personal assistant. The system must detect proximity to the limit and compress, summarize, or truncate intelligently.

## Solution

### Architecture Overview

```
User Message
     │
     ▼
┌─────────────────────────────────────────────────┐
│              Input Guardrail                     │
│  (validate, deduplicate, assign correlation ID)  │
└─────────────┬───────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────┐
│           Bounded Processing Queue               │
│  (max depth, backpressure, priority ordering)    │
└─────────────┬───────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────┐
│          Thinking Pipeline (feature 70)          │
│  Perceive → Enrich → Plan → Execute → Reflect   │
│       ▲              │                           │
│       │         [checkpoint after each stage]    │
│       │              │                           │
│  [restore on    [on failure: notify + degrade]   │
│   restart]           │                           │
└─────────────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────┐
│           Response Delivery                      │
│  (retry, fallback channel, queue if offline)     │
└─────────────────────────────────────────────────┘
```

### Components

#### 1. Bounded Processing Queue (Application)

A `System.Threading.Channels.Channel<T>` with explicit capacity replaces any unbounded in-memory queue. All sources (CLI, Signal, Telegram, Sentinel) enqueue through it.

```csharp
public sealed class ProcessingQueue
{
    private readonly Channel<ProcessingRequest> _channel;

    public ProcessingQueue(IOptions<ResilienceOptions> options)
    {
        _channel = Channel.CreateBounded<ProcessingRequest>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            });
    }

    public async ValueTask EnqueueAsync(
        ProcessingRequest request,
        CancellationToken ct) =>
        await _channel.Writer.WriteAsync(request, ct);

    public IAsyncEnumerable<ProcessingRequest> DequeueAllAsync(
        CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
```

**Sentinel overflow policy:** When the queue is full, Sentinel events are dropped with a warning log. User-initiated messages use `FullMode.Wait` with a 5-second timeout — if the queue is still full, the user gets an immediate "I'm busy, try again shortly" response.

#### 2. Pipeline Stage Recovery (Infrastructure)

Each Executor in the Thinking Pipeline (feature 70) wraps its core logic in a recovery boundary. The framework's `CheckpointManager` provides the persistence layer.

```csharp
// Pattern for every Executor
public sealed class ResilientExecutor<TInput, TOutput>(
    ILogger<ResilientExecutor<TInput, TOutput>> logger)
{
    public async Task<TOutput> ExecuteWithRecoveryAsync(
        Func<TInput, CancellationToken, Task<TOutput>> action,
        Func<TInput, TOutput> degradedFallback,
        TInput input,
        string stageName,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource
            .CreateLinkedTokenSource(ct);

        try
        {
            return await action(input, cts.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // propagate external cancellation
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Stage {StageName} failed, falling back to degraded mode",
                stageName);

            return degradedFallback(input);
        }
    }
}
```

**Stage-specific degradation:**

| Stage | Failure | Degraded Behavior |
|---|---|---|
| **Perceive** | Entity extraction fails | Continue with raw text (no extracted entities) |
| **Enrich** | Memory/Graph unavailable | Continue with conversation history only |
| **Plan** | LLM timeout | Retry once → if fails, use direct-response mode (no tool planning) |
| **Execute** | Tool call fails | Report failure to user, offer retry or skip |
| **Execute** | LLM streaming interrupted | Preserve partial content with `IsComplete = false` |
| **Reflect** | Memory write fails | Log warning, do not block response delivery |

#### 3. LLM Call Resilience (Infrastructure)

All LLM calls go through a single `IResilientLlmClient` that handles retries, timeouts, and provider failover.

```csharp
public interface IResilientLlmClient
{
    Task<ChatResponse> CompleteAsync(
        ChatRequest request,
        CancellationToken ct);

    IAsyncEnumerable<StreamingChatChunk> StreamAsync(
        ChatRequest request,
        CancellationToken ct);
}
```

**Retry policy:**
- Transient errors (429, 503, 5xx): retry up to 3 times with exponential backoff (1s, 4s, 16s)
- Timeout: single retry with 1.5× original timeout
- Auth errors (401, 403): fail immediately, notify user
- Context too long (400 with token error): trigger context compression (see below), retry once

**Provider failover:** If the primary provider (configured via `AiProvider:Provider`) fails 3 consecutive times within 5 minutes, the system enters degraded mode and notifies the user via feature 65. It does not silently switch providers — the user must configure a fallback provider explicitly.

#### 4. Context Window Manager (Application)

Detects when the assembled context (conversation history + enrichment data + system prompt) approaches the model's token limit and compresses it.

```csharp
public interface IContextWindowManager
{
    Task<ContextWindow> AssembleAsync(
        Guid conversationId,
        EnrichmentResult enrichment,
        CancellationToken ct);
}
```

**Strategy (in priority order):**
1. **Drop old enrichment** — remove lowest-relevance memory entries first
2. **Summarize history** — replace messages older than the last 10 turns with an LLM-generated summary, stored as a single system message
3. **Truncate** — as a last resort, drop the oldest messages until under limit

**Token counting:** Use the model's tokenizer (provided by `Microsoft.Extensions.AI`) to count tokens before sending. Maintain a 10% buffer below the model's reported context limit to account for response tokens.

**Summary caching:** Conversation summaries are stored in the `Messages` table with a `Role = "summary"` marker so they are not re-summarized on subsequent requests.

#### 5. RequestPort Timeout Handler (Infrastructure)

All `RequestPort` usages (HITL questions, tool approvals, Sentinel alerts) must have a configurable timeout with explicit default behavior.

```csharp
public sealed record RequestPortOptions
{
    public TimeSpan QuestionTimeout { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan ToolApprovalTimeout { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan SentinelAlertTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
```

**Timeout behavior by request type:**

| Request Type | On Timeout | Rationale |
|---|---|---|
| Question (clarification) | Proceed with best guess, note uncertainty in response | Don't block on optional clarification |
| Tool approval (feature 115) | Deny by default, checkpoint state for later resume | Security: never auto-approve code execution |
| Sentinel alert | Dismiss, log as unacknowledged | Low-priority; user will see it next session |
| Permission request | Deny by default | Security: explicit consent required |

When a timeout occurs, the system emits a `TimeoutEvent` (WorkflowEvent subclass) via the SSE bridge so the user sees what expired.

#### 6. Channel Delivery Resilience (Infrastructure)

Message delivery to CLI, Signal, and Telegram can fail. Each channel has a retry policy and fallback behavior.

```csharp
public interface IResilientChannelDelivery
{
    Task<DeliveryResult> DeliverAsync(
        OutboundMessage message,
        CancellationToken ct);
}

public sealed record DeliveryResult(
    bool Delivered,
    MessageChannel Channel,
    MessageChannel? FallbackUsed,
    string? ErrorReason);
```

**Retry policy per channel:**

| Channel | Retries | Backoff | Fallback |
|---|---|---|---|
| CLI (SSE) | 0 (stateless stream) | N/A | Queue for next connection |
| Signal | 2 | 5s, 15s | Queue + CLI notification |
| Telegram | 2 | 5s, 15s | Queue + CLI notification |

**Offline queue:** When no channel is connected, messages are written to the `ProactiveEvents` table (feature 65) with `DeliveryStatus = Pending`. On next CLI connection, pending events are delivered in order.

#### 7. Health & Readiness Probes (Api)

Extend the existing `/_health` endpoint with component-level health checks.

```csharp
public static class HealthCheckExtensions
{
    public static IServiceCollection AddLeontesHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "database")
            .AddCheck<LlmProviderHealthCheck>("llm-provider")
            .AddCheck<ProcessingQueueHealthCheck>("processing-queue")
            .AddCheck<SentinelHealthCheck>("sentinel-monitors");
}
```

**Health check semantics:**

| Check | Healthy | Degraded | Unhealthy |
|---|---|---|---|
| Database | Connected, < 100ms | Connected, > 1s | Unreachable |
| LLM Provider | Responds to ping | Responds but slow (> 10s) | Unreachable or auth failure |
| Processing Queue | < 50% capacity | 50–90% capacity | > 90% or blocked |
| Sentinel | All enabled monitors running | Some monitors restarting | All monitors failed |

### Error Handling Table

| Scenario | Detection | Recovery | User Notification |
|---|---|---|---|
| LLM timeout | `OperationCanceledException` | Retry once → degraded mode | "I'm having trouble reaching the AI provider" |
| LLM rate limited | 429 response | Exponential backoff (3 retries) | Silent if recovered; notify if exhausted |
| Database connection lost | EF Core exception | Retry with backoff; queue in-memory | "Database temporarily unavailable" |
| Tool execution crash | Exception in forged tool | Catch, log, report to user | "Tool X failed: [safe error message]" |
| Context window exceeded | Token count > limit | Compress context, retry | Silent (automatic compression) |
| Sentinel queue full | `Channel.Writer.TryWrite` returns false | Drop event, log warning | Silent (internal optimization) |
| SSE connection dropped | Write exception | Queue remaining events | Delivered on reconnect |
| RequestPort timeout | `Task.Delay` + cancellation | Apply default behavior per type | "Your approval request for X expired" |
| Checkpoint corruption | Deserialization failure | Start fresh pipeline run, log error | "I had to restart processing your request" |
| Memory consolidation failure | Exception in background service | Skip cycle, retry next interval | Silent (background maintenance) |

### Configuration

```json
{
  "Resilience": {
    "QueueCapacity": 100,
    "Llm": {
      "TimeoutSeconds": 60,
      "MaxRetries": 3,
      "RetryBaseDelaySeconds": 1
    },
    "RequestPort": {
      "QuestionTimeoutMinutes": 10,
      "ToolApprovalTimeoutMinutes": 30,
      "SentinelAlertTimeoutMinutes": 5
    },
    "ContextWindow": {
      "BufferPercentage": 10,
      "SummaryTriggerTurns": 20,
      "MinRecentTurns": 10
    },
    "ChannelDelivery": {
      "MaxRetries": 2,
      "RetryDelaySeconds": 5
    },
    "Sentinel": {
      "MaxEventsPerMinutePerMonitor": 1,
      "QueueDepthLimit": 50
    }
  }
}
```

### Offline / Degraded Mode

When the LLM provider is unreachable, Leontes enters **degraded mode**:

| Capability | Available in Degraded Mode? | How |
|---|---|---|
| Sentinel heuristics | Yes | System 1 runs locally, no LLM needed |
| Sentinel escalation to LLM | No | Events queued until provider returns |
| CLI echo / basic commands | Yes | Direct response without pipeline |
| Memory retrieval | Yes | pgvector queries work independently |
| Memory consolidation | No | Requires LLM for summarization |
| Tool Forge generation | No | Requires LLM for code synthesis |
| Tool Forge execution (existing tools) | Yes | Roslyn-compiled code runs locally |
| Conversation history | Yes | Database queries work independently |

The system checks LLM availability every 30 seconds during degraded mode. On recovery, it processes any queued Sentinel escalations and resumes normal operation. The user is notified of both the transition to degraded mode and the recovery.

## Acceptance Criteria

- [ ] All LLM calls go through `IResilientLlmClient` with retry and timeout
- [ ] Processing queue is bounded with configurable capacity
- [ ] Sentinel events are dropped (not OOM) when queue is full
- [ ] Each pipeline stage has a defined degraded fallback
- [ ] Context window overflow is detected and compressed automatically
- [ ] All `RequestPort` usages have explicit timeouts
- [ ] Tool approval defaults to deny on timeout
- [ ] Channel delivery retries and falls back to offline queue
- [ ] Health endpoint reports component-level status
- [ ] Degraded mode functions without LLM availability
- [ ] Every error surfaces a user-facing notification (no silent failures)
- [ ] Conversation summaries are cached and not re-summarized

## Out of Scope

- Circuit breaker patterns for provider failover (single provider for now)
- Multi-provider load balancing
- Distributed queue (single-instance deployment)
- Self-healing database recovery (rely on PostgreSQL built-in)
