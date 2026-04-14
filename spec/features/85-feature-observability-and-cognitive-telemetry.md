# 85 — Observability & Cognitive Telemetry

## Problem

Leontes runs a 5-stage cognitive pipeline (feature 65), retrieves memories across four types (feature 70), monitors OS events through multiple Sentinel channels (feature 80), and generates tools at runtime (feature 100). When something goes wrong — or when the agent makes a surprising decision — the user has no way to understand why.

Without observability, debugging is guesswork. The user asks "Why did you suggest that?" and the agent can only say "Based on the conversation" rather than "I found 3 matching memories, tool X bid highest, and your Synapse Graph linked Sarah to Project Alpha." A world-class assistant must show its work — not as a research paper, but as a clear trace of what it considered, what it chose, and why.

## Prerequisites

- Working feature 55 (Proactive Communication — event delivery)
- Working feature 65 (Thinking Pipeline — the primary system to observe)
- Working feature 75 (Error Recovery — health checks and degraded mode signals)

## Rules

- All telemetry uses Serilog structured logging — no separate logging framework
- Telemetry must not degrade pipeline latency by more than 5%
- Sensitive data (clipboard content, passwords, file contents) must never appear in telemetry — use hashes or redacted placeholders
- Telemetry is local-only — never sent to external services unless the user explicitly configures an exporter
- Pipeline traces are stored in PostgreSQL, not in-memory — they survive restarts
- Token usage is tracked per request, per stage, and per feature — this feeds into feature 105 (Cost Control)
- Every decision point in the pipeline must emit a structured `DecisionRecord` explaining what was considered and what was chosen

## Background

### The Three Pillars of Observability

Traditional observability focuses on three signals: **logs** (what happened), **metrics** (how much/how fast), and **traces** (the path through the system). For a cognitive system, we add a fourth: **decisions** (why this choice).

| Pillar | What It Captures | Storage |
|---|---|---|
| **Logs** | Events, errors, warnings | Serilog → console + structured file |
| **Metrics** | Token counts, latencies, queue depth, memory hit rates | PostgreSQL (aggregated) |
| **Traces** | Pipeline stage execution with timing and causality | PostgreSQL (per-request) |
| **Decisions** | What the agent considered, ranked, and chose | PostgreSQL (per-request) |

### Why Confidence Scoring Matters

A world-leading agent doesn't just answer — it signals how certain it is. When the agent says "I think Sarah's email is sarah@example.com," the user should know whether that came from the Synapse Graph (high confidence) or a fuzzy memory match (low confidence). Confidence scoring also enables the agent to proactively ask for clarification when uncertainty is high, rather than guessing.

### The "Show Your Work" Principle

The best human assistants explain their reasoning when asked. Leontes should be able to answer "Why did you do that?" by replaying the trace of a specific request — showing the enrichment data retrieved, the tools considered, the plan generated, and the final execution path.

## Solution

### Architecture Overview

```
┌──────────────────────────────────────────────────┐
│               Thinking Pipeline                   │
│                                                   │
│  Perceive ──► Enrich ──► Plan ──► Execute ──► Reflect
│     │           │          │         │           │
│     ▼           ▼          ▼         ▼           ▼
│  [emit]      [emit]     [emit]    [emit]      [emit]
│     │           │          │         │           │
└─────┼───────────┼──────────┼─────────┼───────────┼──┘
      │           │          │         │           │
      ▼           ▼          ▼         ▼           ▼
┌──────────────────────────────────────────────────┐
│            Telemetry Collector                     │
│  (correlates by RequestId, writes to DB)          │
└────────────────────┬─────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
   ┌─────────┐ ┌──────────┐ ┌──────────┐
   │ Traces  │ │ Metrics  │ │Decisions │
   │  Table  │ │  Table   │ │  Table   │
   └─────────┘ └──────────┘ └──────────┘
        │            │            │
        ▼            ▼            ▼
┌──────────────────────────────────────────────────┐
│           Introspection API                       │
│  GET /api/v1/telemetry/traces/{requestId}        │
│  GET /api/v1/telemetry/metrics/summary           │
│  GET /api/v1/telemetry/decisions/{requestId}     │
└──────────────────────────────────────────────────┘
```

### Components

#### 1. Pipeline Trace Record (Domain)

Every pipeline execution produces a trace — a structured record of what each stage did.

```csharp
public sealed record PipelineTrace
{
    public required Guid RequestId { get; init; }
    public required Guid ConversationId { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public required PipelineOutcome Outcome { get; set; }
    public required List<StageTrace> Stages { get; init; } = [];
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
}

public sealed record StageTrace
{
    public required string StageName { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; set; }
    public required StageOutcome Outcome { get; set; }
    public string? ErrorMessage { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public List<DecisionRecord> Decisions { get; init; } = [];
}

public enum PipelineOutcome
{
    Success,
    PartialSuccess,
    DegradedSuccess,
    Failed
}

public enum StageOutcome
{
    Success,
    Skipped,
    Degraded,
    Failed
}
```

#### 2. Decision Record (Domain)

Captures every non-trivial choice the agent makes. This is the "why" behind the agent's behavior.

```csharp
public sealed record DecisionRecord
{
    public required string DecisionType { get; init; }
    public required string Question { get; init; }
    public required string Chosen { get; init; }
    public required List<DecisionCandidate> Candidates { get; init; }
    public required string Rationale { get; init; }
}

public sealed record DecisionCandidate(
    string Name,
    double Score,
    string Reason);
```

**Decision types tracked:**

| Decision Type | Example Question | Example Candidates |
|---|---|---|
| `ToolSelection` | "Which tool to call?" | FileSearch (0.9), WebSearch (0.3), none (0.1) |
| `MemoryRetrieval` | "Which memories are relevant?" | Meeting notes (0.85), Email thread (0.72), Old chat (0.31) |
| `EntityResolution` | "Who is 'Sarah'?" | Sarah Chen (0.95), Sarah Miller (0.45) |
| `ChannelSelection` | "How to deliver this?" | CLI (connected), Signal (available), Telegram (available) |
| `ContextPruning` | "What to drop from context?" | Messages 1-5 (oldest), Memory M3 (lowest relevance) |
| `SentinelEscalation` | "Should this event go to LLM?" | Escalate (surprise: 0.8), Dismiss (surprise: 0.2) |

#### 3. Confidence Score (Domain)

Attached to agent responses to signal certainty level.

```csharp
public sealed record ConfidenceScore(
    double Overall,
    ConfidenceBreakdown Breakdown);

public sealed record ConfidenceBreakdown(
    double MemorySupport,
    double GraphSupport,
    double ConversationClarity,
    double ToolReliability);
```

**Scoring rules:**
- `MemorySupport`: How many relevant memories were found and how similar they were (0 = none, 1 = exact match)
- `GraphSupport`: Whether the Synapse Graph confirmed entities and relationships (0 = no graph data, 1 = fully resolved)
- `ConversationClarity`: Was the user's intent unambiguous? (0 = vague, 1 = explicit command)
- `ToolReliability`: Did the selected tool have a history of success? (0 = first use, 1 = always succeeds)
- `Overall`: Weighted average — `(Memory × 0.25) + (Graph × 0.25) + (Clarity × 0.3) + (Tool × 0.2)`

**Behavioral thresholds:**
- `Overall > 0.8`: Execute confidently
- `0.5 ≤ Overall ≤ 0.8`: Execute but flag uncertainty in response ("I think..." / "Based on what I found...")
- `Overall < 0.5`: Ask for clarification via `RequestPort` before proceeding

#### 4. Telemetry Collector (Infrastructure)

Collects telemetry from pipeline events and writes to PostgreSQL.

```csharp
public interface ITelemetryCollector
{
    Task RecordStageStartAsync(
        Guid requestId, string stageName, CancellationToken ct);

    Task RecordStageCompleteAsync(
        Guid requestId, string stageName, StageOutcome outcome,
        int inputTokens, int outputTokens, CancellationToken ct);

    Task RecordDecisionAsync(
        Guid requestId, string stageName,
        DecisionRecord decision, CancellationToken ct);

    Task RecordConfidenceAsync(
        Guid requestId, ConfidenceScore score, CancellationToken ct);

    Task<PipelineTrace> GetTraceAsync(
        Guid requestId, CancellationToken ct);
}
```

**Implementation:** The collector listens to `WorkflowEvent` subclasses emitted by each Executor (feature 65). It correlates events by `RequestId` and writes batch inserts to minimize database overhead.

#### 5. Metrics Aggregation (Infrastructure)

Periodic aggregation of raw telemetry into summary metrics. Runs as a background service.

```csharp
public sealed record MetricsSummary
{
    public required DateTime PeriodStart { get; init; }
    public required DateTime PeriodEnd { get; init; }
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int DegradedRequests { get; init; }
    public int FailedRequests { get; init; }
    public double MedianLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public int TotalInputTokens { get; init; }
    public int TotalOutputTokens { get; init; }
    public double MemoryHitRate { get; init; }
    public double ToolSuccessRate { get; init; }
    public int SentinelEventsProcessed { get; init; }
    public int SentinelEventsDropped { get; init; }
}
```

**Aggregation periods:** Hourly and daily. Raw traces older than 30 days are pruned; aggregated metrics are kept indefinitely.

#### 6. Introspection API (Api)

Endpoints for the user (or CLI) to query telemetry data.

```
GET  /api/v1/telemetry/traces/{requestId}     → PipelineTrace (full stage-by-stage breakdown)
GET  /api/v1/telemetry/traces                  → PagedResponse<PipelineTraceSummary> (list recent traces)
GET  /api/v1/telemetry/decisions/{requestId}   → List<DecisionRecord> (all decisions for a request)
GET  /api/v1/telemetry/metrics/summary         → MetricsSummary (current period)
GET  /api/v1/telemetry/metrics/history         → PagedResponse<MetricsSummary> (historical)
GET  /api/v1/telemetry/health                  → ComponentHealthReport (from feature 75)
```

**CLI integration:** `leontes trace <requestId>` renders the pipeline trace in a human-readable format. `leontes metrics` shows the current period summary.

#### 7. "Why Did You Do That?" Handler (Application)

When the user asks "why" about a previous response, the system queries the trace for that request and generates a natural-language explanation.

```csharp
public interface IExplainabilityService
{
    Task<string> ExplainAsync(
        Guid requestId, CancellationToken ct);
}
```

**Implementation:** Retrieves the `PipelineTrace` and `DecisionRecord` list, then formats them into a brief explanation without invoking the LLM. Example output:

> "For your last question, I searched 4 memories and found 2 relevant matches (meeting notes from April 3, email thread with Sarah). I used the Synapse Graph to confirm Sarah Chen works on Project Alpha. My confidence was 0.82 — high enough to proceed without asking. I chose the SendEmail tool because it scored highest (0.9) based on your request phrasing."

### Serilog Enrichment

All pipeline log entries are enriched with telemetry context:

```csharp
public sealed class PipelineLogEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        if (Activity.Current is { } activity)
        {
            logEvent.AddPropertyIfAbsent(
                factory.CreateProperty("RequestId", activity.TraceId));
            logEvent.AddPropertyIfAbsent(
                factory.CreateProperty("PipelineStage", activity.DisplayName));
        }
    }
}
```

### Data Model

#### PipelineTraces Table

```sql
CREATE TABLE "PipelineTraces" (
    "Id"                uuid PRIMARY KEY,
    "RequestId"         uuid NOT NULL,
    "ConversationId"    uuid NOT NULL,
    "StartedAt"         timestamptz NOT NULL,
    "CompletedAt"       timestamptz,
    "Outcome"           text NOT NULL,
    "TotalInputTokens"  int NOT NULL DEFAULT 0,
    "TotalOutputTokens" int NOT NULL DEFAULT 0,
    "Created"           timestamptz NOT NULL,
    "CreatedBy"         uuid NOT NULL
);

CREATE INDEX "IX_PipelineTraces_RequestId" ON "PipelineTraces" ("RequestId");
CREATE INDEX "IX_PipelineTraces_ConversationId" ON "PipelineTraces" ("ConversationId");
CREATE INDEX "IX_PipelineTraces_StartedAt" ON "PipelineTraces" ("StartedAt" DESC);
```

#### StageTraces Table

```sql
CREATE TABLE "StageTraces" (
    "Id"              uuid PRIMARY KEY,
    "PipelineTraceId" uuid NOT NULL REFERENCES "PipelineTraces"("Id"),
    "StageName"       text NOT NULL,
    "StartedAt"       timestamptz NOT NULL,
    "CompletedAt"     timestamptz,
    "Outcome"         text NOT NULL,
    "ErrorMessage"    text,
    "InputTokens"     int NOT NULL DEFAULT 0,
    "OutputTokens"    int NOT NULL DEFAULT 0,
    "Created"         timestamptz NOT NULL,
    "CreatedBy"       uuid NOT NULL
);

CREATE INDEX "IX_StageTraces_PipelineTraceId" ON "StageTraces" ("PipelineTraceId");
```

#### DecisionRecords Table

```sql
CREATE TABLE "DecisionRecords" (
    "Id"            uuid PRIMARY KEY,
    "StageTraceId"  uuid NOT NULL REFERENCES "StageTraces"("Id"),
    "DecisionType"  text NOT NULL,
    "Question"      text NOT NULL,
    "Chosen"        text NOT NULL,
    "Candidates"    jsonb NOT NULL,
    "Rationale"     text NOT NULL,
    "Created"       timestamptz NOT NULL,
    "CreatedBy"     uuid NOT NULL
);

CREATE INDEX "IX_DecisionRecords_StageTraceId" ON "DecisionRecords" ("StageTraceId");
```

#### MetricsSummaries Table

```sql
CREATE TABLE "MetricsSummaries" (
    "Id"                        uuid PRIMARY KEY,
    "PeriodStart"               timestamptz NOT NULL,
    "PeriodEnd"                 timestamptz NOT NULL,
    "TotalRequests"             int NOT NULL,
    "SuccessfulRequests"        int NOT NULL,
    "DegradedRequests"          int NOT NULL,
    "FailedRequests"            int NOT NULL,
    "MedianLatencyMs"           double precision NOT NULL,
    "P95LatencyMs"              double precision NOT NULL,
    "TotalInputTokens"          int NOT NULL,
    "TotalOutputTokens"         int NOT NULL,
    "MemoryHitRate"             double precision NOT NULL,
    "ToolSuccessRate"           double precision NOT NULL,
    "SentinelEventsProcessed"   int NOT NULL,
    "SentinelEventsDropped"     int NOT NULL,
    "Created"                   timestamptz NOT NULL,
    "CreatedBy"                 uuid NOT NULL
);

CREATE INDEX "IX_MetricsSummaries_PeriodStart" ON "MetricsSummaries" ("PeriodStart" DESC);
```

### Migration

```bash
dotnet ef migrations add AddObservabilityTables \
    --project backend/src/Leontes.Infrastructure \
    --startup-project backend/src/Leontes.Api
```

### Configuration

```json
{
  "Telemetry": {
    "Enabled": true,
    "TraceRetentionDays": 30,
    "MetricsAggregationIntervalMinutes": 60,
    "ConfidenceThresholds": {
      "HighConfidence": 0.8,
      "LowConfidence": 0.5
    },
    "SensitiveFieldPatterns": [
      "password", "secret", "token", "key", "credential",
      "iban", "credit_card", "ssn"
    ]
  }
}
```

## Acceptance Criteria

- [ ] Every pipeline execution produces a `PipelineTrace` with per-stage timing
- [ ] Every non-trivial decision produces a `DecisionRecord` with candidates and rationale
- [ ] Agent responses include a `ConfidenceScore` that influences behavior (ask vs. proceed)
- [ ] Token usage is tracked per stage and per request
- [ ] Introspection API exposes traces, decisions, and metrics
- [ ] CLI supports `leontes trace` and `leontes metrics` commands
- [ ] "Why did you do that?" produces a natural-language explanation from stored trace data
- [ ] Sensitive data is never present in telemetry (hashed/redacted)
- [ ] Aggregated metrics are computed hourly and stored permanently
- [ ] Raw traces older than retention period are automatically pruned
- [ ] Serilog entries include `RequestId` and `PipelineStage` enrichment
- [ ] Telemetry overhead does not degrade pipeline latency by more than 5%

## Out of Scope

- External telemetry exporters (OpenTelemetry, Datadog, Grafana) — future enhancement
- Visual dashboard (web UI for traces) — covered by AG-UI in feature 110
- Real-time streaming of telemetry events — use SSE events from feature 55
- A/B testing of different pipeline configurations
