# 100 — Cost Control & Budget Management

## Problem

Leontes makes LLM calls from multiple subsystems: the Thinking Pipeline (feature 70) calls the LLM in both Plan and Execute stages, memory consolidation (feature 80) calls the LLM hourly to distill observations into insights, Sentinel escalation (feature 90) sends surprising events to the LLM for interpretation, and Tool Forge (feature 115) calls the LLM to generate and fix code. Each of these runs independently with no shared awareness of total spend.

Without cost control, a busy day — many conversations, frequent Sentinel events, and a Tool Forge generation cycle — can consume thousands of tokens with no visibility or brakes. Even with a free local model (Ollama), token throughput is finite and shared across features. With a cloud provider, the cost is real money. A world-class assistant manages its own resource consumption, prioritizes interactive requests over background tasks, and tells the user when it's running hot.

## Prerequisites

- Working feature 65 (Thinking Pipeline — token tracking per stage)
- Working feature 95 (Observability — token metrics collection and storage)

## Rules

- Token tracking is mandatory for every LLM call — no unmetered calls
- Budget limits are soft by default (warn, then throttle) — only the user can set a hard stop
- Interactive requests (user-initiated chat) always take priority over background tasks (consolidation, Sentinel escalation, Tool Forge)
- Model routing decisions are transparent — the user knows which model handled their request
- Budget configuration is stored in the database (not config files) — changeable at runtime
- Cost estimates use provider-reported pricing or user-configured rates — no hardcoded prices
- All budget math uses token counts, not dollar amounts — dollar conversion is display-only

## Background

### The Token Economy

Every LLM interaction has a cost measured in tokens (input + output). Different features consume tokens at very different rates:

| Feature | Frequency | Tokens per Call (typical) | Priority |
|---|---|---|---|
| Chat (Execute stage) | Per user message | 500–4000 | Critical |
| Plan stage | Per user message | 200–1000 | Critical |
| Memory consolidation | Hourly | 1000–3000 | Low |
| Sentinel escalation | Per event (filtered) | 200–800 | Medium |
| Tool Forge generation | Per tool request | 2000–5000 | Low |
| Tool Forge fix attempts | Per failed test | 1000–3000 | Low |
| Context summarization | When context overflows | 1000–2000 | Medium |
| Explainability ("why?") | Per user request | 0 (no LLM, uses stored trace) | N/A |

### Model Routing: Right Model for the Job

Not every LLM call needs the most capable (and expensive) model. Simple tasks like entity extraction, classification, and memory summarization can use a smaller, faster model. Complex tasks like multi-step planning and code generation benefit from a larger model.

| Task | Model Tier | Rationale |
|---|---|---|
| Entity extraction (Perceive) | Small / no LLM | Pattern matching + NER, heuristics sufficient |
| Memory consolidation | Small | Summarization is well-suited for smaller models |
| Sentinel classification | Small | Simple category assignment |
| Context summarization | Small | Summarization task |
| Plan generation | Large | Requires complex reasoning |
| Chat response (Execute) | Large | User-facing quality matters |
| Tool Forge code generation | Large | Code quality and correctness critical |

### Budget Periods

Budgets are tracked in rolling 24-hour windows. This prevents "saving up" unused budget from quiet days and spending it all at once, while also preventing a burst early in the day from locking out the agent for the remaining hours.

## Solution

### Architecture Overview

```
┌──────────────────────────────────────────────────┐
│               Token Meter                         │
│  (wraps every LLM call, records usage)           │
└─────────────┬────────────────────────────────────┘
              │
              ▼
┌──────────────────────────────────────────────────┐
│            Budget Manager                         │
│                                                   │
│  ┌──────────────┐  ┌────────────┐  ┌───────────┐ │
│  │ Token Ledger │  │  Throttle  │  │  Model    │ │
│  │  (per call)  │  │  Engine    │  │  Router   │ │
│  └──────────────┘  └────────────┘  └───────────┘ │
│         │                │               │        │
│         ▼                ▼               ▼        │
│  ┌──────────────────────────────────────────────┐│
│  │        Budget Policy (per feature)           ││
│  │  Chat: 60%  Sentinel: 15%  Consolidation: 15%│
│  │  ToolForge: 10%                              ││
│  └──────────────────────────────────────────────┘│
└──────────────────────────────────────────────────┘
```

### Components

#### 1. Token Meter (Infrastructure)

Implemented as an `IChatClient` decorator using the Microsoft.Extensions.AI middleware pattern. Every `IChatClient` call flows through the meter, which reads token usage from `ChatResponse.Usage` (provided by M.E.AI) and records it.

```csharp
public interface ITokenMeter
{
    Task<MeteredResponse<T>> MeterAsync<T>(
        string feature,
        string operation,
        Func<CancellationToken, Task<T>> llmCall,
        CancellationToken ct);
}

public sealed record MeteredResponse<T>(
    T Response,
    TokenUsage Usage);

public sealed record TokenUsage(
    string Feature,
    string Operation,
    string ModelId,
    int InputTokens,
    int OutputTokens,
    DateTime Timestamp);
```

**How token counts are obtained:** `Microsoft.Extensions.AI` provides `ChatResponse.Usage` with `InputTokenCount`, `OutputTokenCount`, and `TotalTokenCount` on every response. For streaming via `GetStreamingResponseAsync()`, the final `ChatResponseUpdate` contains a `UsageContent` in its `Contents` collection. No custom token counting needed — the provider reports actual usage.

**Observability layer:** Wrapping `ChatClientAgent` with the Agent Framework's `OpenTelemetryAgent` decorator automatically records token counts as OpenTelemetry span attributes and metrics. This provides the observability layer (dashboards, alerting) while `ITokenMeter` provides the synchronous budget enforcement layer.

**Integration:** The `IResilientLlmClient` (feature 85) calls `ITokenMeter` internally. No LLM call bypasses metering.

#### 2. Token Ledger (Infrastructure)

Persistent record of all token usage. Written on every LLM call, queried for budget checks and reporting.

```csharp
public interface ITokenLedger
{
    Task RecordUsageAsync(TokenUsage usage, CancellationToken ct);

    Task<TokenBudgetStatus> GetBudgetStatusAsync(
        string feature, CancellationToken ct);

    Task<TokenBudgetStatus> GetGlobalBudgetStatusAsync(
        CancellationToken ct);

    Task<List<TokenUsageSummary>> GetUsageHistoryAsync(
        DateTime from, DateTime to, CancellationToken ct);
}

public sealed record TokenBudgetStatus(
    string Feature,
    int TokensUsed,
    int TokenBudget,
    double PercentUsed,
    BudgetState State);

public enum BudgetState
{
    Normal,
    Warning,
    Throttled,
    Exhausted
}
```

#### 3. Budget Policy (Domain)

Defines how the total daily token budget is allocated across features.

```csharp
public sealed class BudgetPolicy : Entity
{
    public int DailyTokenBudget { get; set; } = 500_000;
    public int WarningThresholdPercent { get; set; } = 75;
    public int ThrottleThresholdPercent { get; set; } = 90;
    public bool HardStopEnabled { get; set; }
    public int HardStopThresholdPercent { get; set; } = 100;

    public Dictionary<string, int> FeatureAllocations { get; set; } = new()
    {
        ["Chat"] = 60,
        ["Sentinel"] = 15,
        ["Consolidation"] = 15,
        ["ToolForge"] = 10
    };
}
```

**Feature allocation:** Percentages of the daily budget. If Chat has used its 60% allocation, it can still borrow from unused allocations of other features — the per-feature split is a soft target, not a hard wall. Only the global budget triggers throttling.

#### 4. Throttle Engine (Application)

Enforces budget limits by delaying or denying low-priority LLM calls when the budget is stressed.

```csharp
public interface IThrottleEngine
{
    Task<ThrottleDecision> EvaluateAsync(
        string feature,
        string operation,
        int estimatedTokens,
        CancellationToken ct);
}

public sealed record ThrottleDecision(
    bool Allowed,
    TimeSpan? DelayBefore,
    string? DenialReason,
    string? SuggestedModel);
```

**Throttle behavior by budget state:**

| Budget State | Interactive (Chat) | Background (Consolidation, Sentinel, Forge) |
|---|---|---|
| **Normal** (< 75%) | Proceed immediately | Proceed immediately |
| **Warning** (75–90%) | Proceed, notify user | Proceed with delay (30s between calls) |
| **Throttled** (90–100%) | Proceed, warn user | Defer to next period or skip |
| **Exhausted** (≥ 100%, hard stop on) | Deny with explanation | Deny |
| **Exhausted** (≥ 100%, hard stop off) | Proceed with strong warning | Deny |

**Notifications:** At Warning and Throttled thresholds, the system emits a `BudgetWarningEvent` via the proactive communication channel (feature 65). The message includes current usage, remaining budget, and which features are consuming the most.

#### 5. Model Router (Infrastructure)

Selects the optimal model for each LLM call based on task complexity and budget state.

```csharp
public interface IModelRouter
{
    Task<ModelSelection> SelectModelAsync(
        ModelRoutingContext context,
        CancellationToken ct);
}

public sealed record ModelRoutingContext(
    string Feature,
    string Operation,
    ModelTier PreferredTier,
    int EstimatedInputTokens,
    BudgetState CurrentBudgetState);

public sealed record ModelSelection(
    string ModelId,
    ModelTier Tier,
    string Reason);

public enum ModelTier
{
    Small,
    Large
}
```

**Relationship to feature 75 (Agent Persona & Model Configuration):** Feature 75 defines the static per-stage model tier assignment (Plan → Large, Reflect → Small, etc.) and registers two keyed `IChatClient` instances. The `IModelRouter` here is the budget-aware layer on top: it can override a stage's preferred tier when the budget is stressed.

**Routing rules:**
1. If budget state is Normal → use preferred tier (from feature 75 stage config)
2. If budget state is Warning → downgrade background tasks to Small tier
3. If budget state is Throttled → downgrade all tasks to Small tier
4. User-facing chat is never downgraded without notification — the user sees "Using a faster model to conserve budget"

**Model configuration** is defined in `AiProvider:Models` (feature 75). Cost-specific fields are added here:

```json
{
  "CostControl": {
    "ModelCosts": {
      "Large": {
        "InputTokenCost": 0,
        "OutputTokenCost": 0
      },
      "Small": {
        "InputTokenCost": 0,
        "OutputTokenCost": 0
      }
    }
  }
}
```

**Cost fields:** Set to `0` for local models (Ollama). When using cloud providers, set to the provider's per-token pricing (in millionths of a dollar per token) for accurate cost estimation.

#### 6. Cost Dashboard Data (Application)

Provides data for CLI display and future AG-UI dashboard.

```csharp
public interface ICostDashboard
{
    Task<DailyBudgetReport> GetTodayAsync(CancellationToken ct);
    Task<List<DailyBudgetReport>> GetHistoryAsync(
        int days, CancellationToken ct);
}

public sealed record DailyBudgetReport(
    DateTime Date,
    int TotalTokensUsed,
    int DailyBudget,
    double PercentUsed,
    Dictionary<string, FeatureUsageReport> ByFeature,
    decimal? EstimatedCostUsd);

public sealed record FeatureUsageReport(
    string Feature,
    int InputTokens,
    int OutputTokens,
    int CallCount,
    string PrimaryModel);
```

**CLI integration:**

```bash
leontes budget                    # Today's usage summary
leontes budget history [--days 7] # Usage history
leontes budget set 500000         # Set daily token budget
leontes budget allocate           # Interactive feature allocation editor
```

Example CLI output:
```
Budget: 342,180 / 500,000 tokens (68.4%) ■■■■■■■░░░
                                          ▲ Normal

  Chat          210,400 (61.5%)  ████████████▌
  Sentinel       48,200 (14.1%)  ███░
  Consolidation  62,080 (18.1%)  ████░
  Tool Forge     21,500 ( 6.3%)  ██░

Model: llama3.1:70b (Large) — 47 calls today
Est. cost: $0.00 (local model)
```

### Data Model

#### TokenUsageRecords Table

```sql
CREATE TABLE "TokenUsageRecords" (
    "Id"            uuid PRIMARY KEY,
    "Feature"       text NOT NULL,
    "Operation"     text NOT NULL,
    "ModelId"       text NOT NULL,
    "InputTokens"   int NOT NULL,
    "OutputTokens"  int NOT NULL,
    "Timestamp"     timestamptz NOT NULL,
    "Created"       timestamptz NOT NULL,
    "CreatedBy"     uuid NOT NULL
);

CREATE INDEX "IX_TokenUsageRecords_Timestamp" ON "TokenUsageRecords" ("Timestamp" DESC);
CREATE INDEX "IX_TokenUsageRecords_Feature_Timestamp"
    ON "TokenUsageRecords" ("Feature", "Timestamp" DESC);
```

#### BudgetPolicies Table

```sql
CREATE TABLE "BudgetPolicies" (
    "Id"                        uuid PRIMARY KEY,
    "DailyTokenBudget"          int NOT NULL DEFAULT 500000,
    "WarningThresholdPercent"   int NOT NULL DEFAULT 75,
    "ThrottleThresholdPercent"  int NOT NULL DEFAULT 90,
    "HardStopEnabled"           boolean NOT NULL DEFAULT false,
    "HardStopThresholdPercent"  int NOT NULL DEFAULT 100,
    "FeatureAllocations"        jsonb NOT NULL,
    "Created"                   timestamptz NOT NULL,
    "CreatedBy"                 uuid NOT NULL,
    "LastModified"              timestamptz,
    "LastModifiedBy"            uuid
);
```

### Token Usage Retention

Raw `TokenUsageRecords` older than 90 days are aggregated into `MetricsSummaries` (feature 95) and then deleted. Aggregated data is kept indefinitely for trend analysis.

### Migration

```bash
dotnet ef migrations add AddCostControlTables \
    --project backend/src/Leontes.Infrastructure \
    --startup-project backend/src/Leontes.Api
```

### Configuration

```json
{
  "CostControl": {
    "DailyTokenBudget": 500000,
    "WarningThresholdPercent": 75,
    "ThrottleThresholdPercent": 90,
    "HardStopEnabled": false,
    "FeatureAllocations": {
      "Chat": 60,
      "Sentinel": 15,
      "Consolidation": 15,
      "ToolForge": 10
    },
    "Models": {
      "Large": {
        "Provider": "ollama",
        "ModelId": "llama3.1:70b",
        "InputTokenCost": 0,
        "OutputTokenCost": 0
      },
      "Small": {
        "Provider": "ollama",
        "ModelId": "llama3.1:8b",
        "InputTokenCost": 0,
        "OutputTokenCost": 0
      }
    },
    "UsageRetentionDays": 90
  }
}
```

### Error Handling

| Scenario | Behavior |
|---|---|
| Token counting unavailable (model doesn't report) | Estimate from character count (1 token ≈ 4 chars), log warning |
| Budget database unreachable | Allow all calls (fail-open), log error, notify user |
| Feature allocation doesn't sum to 100% | Normalize proportionally at load time |
| Unknown feature name in LLM call | Record under "Other", log warning |
| Model router can't reach preferred model | Fall back to available model, record in decision log (feature 95) |

## Acceptance Criteria

- [ ] Every LLM call is metered — no untracked token usage
- [ ] Token usage is recorded per feature, operation, and model
- [ ] Daily budget with configurable limit is enforced
- [ ] Warning notification at 75% budget usage
- [ ] Background tasks are throttled at 90% budget usage
- [ ] Hard stop (when enabled) blocks all LLM calls at configured threshold
- [ ] Interactive chat is never silently blocked — always warns first
- [ ] Model router selects appropriate tier based on task and budget state
- [ ] User is notified when model is downgraded due to budget pressure
- [ ] CLI `leontes budget` shows current usage with visual bar
- [ ] Budget policy is stored in database and changeable at runtime
- [ ] Token usage records are retained for 90 days, then aggregated
- [ ] Feature allocations are soft targets — borrowing from unused allocations is allowed

## Out of Scope

- Real-time dollar cost tracking with live provider pricing APIs
- Multi-provider cost comparison and optimization
- Predictive budget forecasting ("at this rate, you'll hit the limit by 3 PM")
- Per-conversation or per-tool budget limits
- Automatic budget adjustment based on usage patterns
