# 70 — Thinking Pipeline

## Problem

The current Processing Loop is a linear request-response cycle: receive message → call LLM → return response. This works for simple chat but cannot support memory enrichment, reflection, context-aware reasoning, or mid-task human interaction. The assistant needs a staged cognitive pipeline that mirrors how deliberate human thought works — perceive the situation, recall relevant context, plan an approach, execute it, and reflect on the outcome — with the ability to pause for human input at any point.

## Prerequisites

- Working CLI chat with SSE streaming (feature 10)
- API key authentication (feature 40)
- Proactive Communication infrastructure (feature 65) — for RequestPort and WorkflowEvent bridging
- Agent Persona & Model Configuration (feature 75) — persona instructions, per-stage model tier and temperature

## Rules

- Built on `Microsoft.Agents.AI.Workflows` — each cognitive stage is a Workflow Executor, connected by Edges
- Stages execute serially for a given message to ensure state consistency (Lane Queue principle)
- The workflow uses `CheckpointManager` to persist state between stages — the pipeline survives server restarts
- Any stage can pause the workflow via `RequestPort` to ask the user a question (feature 65)
- Any stage can emit `WorkflowEvent` subclasses for progress/observability (feature 65)
- Stages that have no work to do (e.g., Enrich when memory is empty) must be no-ops, not bottlenecks
- The pipeline must be extensible — adding a new stage means adding an Executor and an Edge

## Background

### Why Agent Framework Workflows Instead of Custom Pipeline

A custom `IPipelineStage` interface with `IEnumerable<IPipelineStage>` ordering is simple but limited:

- No built-in state persistence — if the server restarts mid-pipeline, all context is lost
- No native mechanism for pausing to ask the user a question
- No standardized event emission for observability
- No framework-level support for conditional branching or retry

The Microsoft Agent Framework's Workflow engine provides all of these via `Executor` + `Edge` + `RequestPort` + `CheckpointManager`. Each cognitive stage becomes an Executor — a self-contained unit of work with typed input/output. Edges connect them in sequence. The framework handles serialization, checkpointing, event emission, and human-in-the-loop pausing automatically.

### Dual-Process Theory (Kahneman)

Human cognition operates on two systems:
- **System 1** (fast, reactive): Pattern matching, reflexes, heuristics — handled by Sentinel (feature 90)
- **System 2** (slow, deliberate): Planning, reasoning, reflection — handled by this pipeline

The Thinking Pipeline is System 2. It only activates when System 1 (Sentinel) escalates a task or when the user explicitly sends a message. This separation prevents expensive LLM calls for tasks that simple heuristics can handle.

### Global Workspace Theory (Dehaene)

Consciousness operates as a shared workspace where specialized modules broadcast information. The pipeline stages approximate this: each Executor enriches a shared `ThinkingContext` that accumulates perception, memory, plan, and results as it flows through the Workflow graph. The `ThinkingContext` is the Global Workspace — every Executor can read what previous ones contributed and add its own contribution.

### Lane Queue Principle (OpenClaw)

Every user intent is a "Task" in a lane. It executes serially to ensure state consistency. This maps directly to the Workflow's execution model — each `StreamingRun` processes one user message through all stages in sequence. Multiple concurrent messages are queued at the Processing Loop level, not inside the pipeline.

## Solution

### Architecture Overview

```
User Message (CLI/Signal/Telegram)
    │
    ▼
┌──────────────────────────────────────────────────────────┐
│  Thinking Workflow (Microsoft.Agents.AI.Workflows)        │
│                                                          │
│  ┌─────────┐    ┌────────┐    ┌──────┐    ┌─────────┐   │
│  │Perceive │───▶│Enrich  │───▶│Plan  │───▶│Execute  │   │
│  │Executor │    │Executor│    │Exec. │    │Executor │   │
│  └─────────┘    └────────┘    └──┬───┘    └────┬────┘   │
│       │              │           │              │        │
│       │emit          │emit       │RequestPort   │stream  │
│       ▼              ▼           │(optional)    ▼        │
│  ProgressEvent  ProgressEvent    │         SSE tokens    │
│                                  ▼                       │
│                            QuestionPort ──────────┐      │
│                            (HITL pause)           │      │
│                                                   │      │
│  ┌─────────┐◀─────────────────────────────────────┘      │
│  │Reflect  │                                             │
│  │Executor │                                             │
│  └─────────┘                                             │
│       │                                                  │
│       │emit                                              │
│       ▼                                                  │
│  InsightEvent                                            │
│                                                          │
│  CheckpointManager ── saves state after each stage ──    │
└──────────────────────────────────────────────────────────┘
    │
    ▼
WorkflowOutputEvent → Response (via IWorkflowEventBridge)
```

### Components

#### 1. ThinkingContext (Domain Layer)

A serializable context object that flows through all Executors via the Workflow state. Must be JSON-serializable for checkpointing:

```csharp
public sealed class ThinkingContext
{
    public required Guid MessageId { get; init; }
    public required Guid ConversationId { get; init; }
    public required string UserContent { get; init; }
    public required string Channel { get; init; }

    // Populated by Perceive
    public string? Intent { get; set; }
    public IReadOnlyList<string> ExtractedEntities { get; set; } = [];
    public MessageUrgency Urgency { get; set; } = MessageUrgency.Normal;

    // Populated by Enrich
    public IReadOnlyList<RelevantMemory> RelevantMemories { get; set; } = [];
    public IReadOnlyList<HistoryMessage> ConversationHistory { get; set; } = [];
    public IReadOnlyList<ResolvedEntity> ResolvedEntities { get; set; } = [];

    // Populated by Plan
    public string? Plan { get; set; }
    public IReadOnlyList<string> SelectedTools { get; set; } = [];
    public bool RequiresHumanInput { get; set; }
    public string? HumanInputQuestion { get; set; }
    public string? HumanInputResponse { get; set; }

    // Populated by Execute
    public string? Response { get; set; }
    public bool IsComplete { get; set; }
    public IReadOnlyList<ToolCallResult> ToolResults { get; set; } = [];

    // Populated by Reflect
    public IReadOnlyList<string> NewInsights { get; set; } = [];
    public IReadOnlyList<EntityUpdate> GraphUpdates { get; set; } = [];
}

public enum MessageUrgency { Low, Normal, High, Critical }

public sealed record RelevantMemory(
    Guid MemoryId, string Content, MemoryType Type, double Relevance);

public sealed record HistoryMessage(
    string Role, string Content, DateTime Timestamp);

public sealed record ResolvedEntity(
    string Mention, Guid EntityId, string EntityType, string ResolvedName);

public sealed record ToolCallResult(
    string ToolName, string Input, string Output, bool Success);

public sealed record EntityUpdate(
    Guid EntityId, string RelationType, Guid RelatedEntityId);
```

#### 2. Workflow Executors (Infrastructure Layer)

Each cognitive stage is an Executor — a self-contained processing unit:

```csharp
// Base contract — each executor transforms ThinkingContext
// The Executor base class comes from Microsoft.Agents.AI.Workflows

public sealed class PerceiveExecutor(
    ILogger<PerceiveExecutor> logger) : Executor<ThinkingContext, ThinkingContext>
{
    public override string Name => "Perceive";

    protected override async Task<ThinkingContext> ExecuteAsync(
        ThinkingContext input,
        ExecutorContext context,
        CancellationToken cancellationToken)
    {
        await context.AddEventAsync(
            new ProgressEvent("Perceive", "Parsing intent and entities...", 0.0));

        // Lightweight — no LLM call
        input.ExtractedEntities = EntityExtractor.Extract(input.UserContent);
        input.Intent = IntentClassifier.Classify(input.UserContent);
        input.Urgency = UrgencyDetector.Detect(input.UserContent, input.Channel);

        logger.LogDebug(
            "Perceived intent {Intent} with {EntityCount} entities, urgency {Urgency}",
            input.Intent, input.ExtractedEntities.Count, input.Urgency);

        return input;
    }
}

public sealed class EnrichExecutor(
    IMemoryStore memoryStore,
    IApplicationDbContext dbContext,
    ISynapseGraph synapseGraph,
    ILogger<EnrichExecutor> logger) : Executor<ThinkingContext, ThinkingContext>
{
    public override string Name => "Enrich";

    protected override async Task<ThinkingContext> ExecuteAsync(
        ThinkingContext input,
        ExecutorContext context,
        CancellationToken cancellationToken)
    {
        await context.AddEventAsync(
            new ProgressEvent("Enrich", "Searching memory for relevant context...", 0.2));

        // Query conversation history
        input.ConversationHistory = await LoadHistoryAsync(
            input.ConversationId, cancellationToken);

        // Query episodic + semantic memory via vector similarity
        if (memoryStore is not null)
        {
            input.RelevantMemories = await memoryStore.SearchAsync(
                input.UserContent, limit: 10, cancellationToken);
        }

        // Resolve entity mentions via Synapse Graph
        foreach (var entity in input.ExtractedEntities)
        {
            var resolved = await synapseGraph.ResolveAsync(entity, cancellationToken);
            if (resolved is not null)
            {
                input.ResolvedEntities = [..input.ResolvedEntities, resolved];
            }
        }

        await context.AddEventAsync(
            new ProgressEvent("Enrich",
                $"Found {input.RelevantMemories.Count} memories, " +
                $"resolved {input.ResolvedEntities.Count} entities", 0.4));

        return input;
    }
}

public sealed class PlanExecutor(
    IChatClient chatClient,
    ILogger<PlanExecutor> logger) : Executor<ThinkingContext, ThinkingContext>
{
    public override string Name => "Plan";

    protected override async Task<ThinkingContext> ExecuteAsync(
        ThinkingContext input,
        ExecutorContext context,
        CancellationToken cancellationToken)
    {
        await context.AddEventAsync(
            new ProgressEvent("Plan", "Determining approach...", 0.5));

        // Build planning prompt with enriched context
        var planningPrompt = PlanningPromptBuilder.Build(input);

        var response = await chatClient.GetResponseAsync(
            planningPrompt, cancellationToken: cancellationToken);

        input.Plan = response.Text;
        input.SelectedTools = ToolSelector.FromPlan(response.Text);

        // If the plan requires clarification, use RequestPort
        if (input.RequiresHumanInput && input.HumanInputQuestion is not null)
        {
            await context.SendMessageAsync(
                new QuestionRequest(
                    "Clarification needed",
                    input.HumanInputQuestion,
                    Options: null,
                    Timeout: TimeSpan.FromMinutes(5)));
            // Workflow pauses here — resumes when human responds
            // Response arrives in input.HumanInputResponse via checkpoint restore
        }

        return input;
    }
}

public sealed class ExecuteExecutor(
    IChatClient chatClient,
    IEnumerable<AITool> tools,
    ILogger<ExecuteExecutor> logger) : Executor<ThinkingContext, ThinkingContext>
{
    public override string Name => "Execute";

    protected override async Task<ThinkingContext> ExecuteAsync(
        ThinkingContext input,
        ExecutorContext context,
        CancellationToken cancellationToken)
    {
        await context.AddEventAsync(
            new ProgressEvent("Execute", "Generating response...", 0.6));

        // Build the full prompt with plan + context
        var messages = ExecutionPromptBuilder.Build(input);

        // Stream response tokens — each emitted as a WorkflowEvent
        var streamingResponse = chatClient.GetStreamingResponseAsync(
            messages, new() { Tools = tools }, cancellationToken);

        var responseBuilder = new StringBuilder();
        await foreach (var update in streamingResponse)
        {
            foreach (var content in update.Contents)
            {
                if (content is TextContent text)
                {
                    responseBuilder.Append(text.Text);
                    await context.AddEventAsync(
                        new AgentResponseUpdateEvent(text.Text));
                }
            }
        }

        input.Response = responseBuilder.ToString();
        input.IsComplete = true;

        await context.AddEventAsync(
            new ProgressEvent("Execute", "Response complete", 1.0));

        return input;
    }
}

public sealed class ReflectExecutor(
    IMemoryStore memoryStore,
    ISynapseGraph synapseGraph,
    ILogger<ReflectExecutor> logger) : Executor<ThinkingContext, ThinkingContext>
{
    public override string Name => "Reflect";

    protected override async Task<ThinkingContext> ExecuteAsync(
        ThinkingContext input,
        ExecutorContext context,
        CancellationToken cancellationToken)
    {
        if (!input.IsComplete)
        {
            logger.LogWarning(
                "Skipping reflection for incomplete response on message {MessageId}",
                input.MessageId);
            return input;
        }

        // Extract insights worth remembering
        input.NewInsights = InsightExtractor.Extract(input);

        // Store new episodic memory
        if (memoryStore is not null)
        {
            await memoryStore.StoreEpisodicAsync(
                input.ConversationId,
                input.UserContent,
                input.Response!,
                cancellationToken);
        }

        // Update Synapse Graph relationships
        foreach (var update in input.GraphUpdates)
        {
            await synapseGraph.AddRelationshipAsync(
                update.EntityId, update.RelationType, update.RelatedEntityId,
                cancellationToken);
        }

        if (input.NewInsights.Count > 0)
        {
            await context.AddEventAsync(
                new InsightEvent(
                    string.Join("; ", input.NewInsights),
                    "Reflection"));
        }

        return input;
    }
}
```

#### 3. Workflow Definition (Application Layer)

The pipeline is defined as a Workflow graph using the Builder API:

```csharp
public static class ThinkingWorkflowDefinition
{
    public static Workflow<ThinkingContext, ThinkingContext> Build(
        IServiceProvider services)
    {
        var perceive = services.GetRequiredService<PerceiveExecutor>();
        var enrich = services.GetRequiredService<EnrichExecutor>();
        var plan = services.GetRequiredService<PlanExecutor>();
        var execute = services.GetRequiredService<ExecuteExecutor>();
        var reflect = services.GetRequiredService<ReflectExecutor>();

        var builder = new WorkflowBuilder<ThinkingContext, ThinkingContext>();

        builder
            .AddExecutor(perceive)
            .AddExecutor(enrich)
            .AddExecutor(plan)
            .AddExecutor(execute)
            .AddExecutor(reflect)
            .AddEdge(perceive, enrich)
            .AddEdge(enrich, plan)
            .AddEdge(plan, execute)
            .AddEdge(execute, reflect)
            .SetEntryPoint(perceive)
            .SetExitPoint(reflect);

        return builder.Build();
    }
}
```

#### 4. Workflow Host (Api Layer)

The Processing Loop hosts the Workflow execution with checkpointing:

```csharp
public sealed class ThinkingWorkflowHost(
    Workflow<ThinkingContext, ThinkingContext> workflow,
    CheckpointManager checkpointManager,
    IWorkflowEventBridge eventBridge,
    IWorkflowSessionManager sessionManager,
    ILogger<ThinkingWorkflowHost> logger)
{
    public async Task<ThinkingContext> ProcessAsync(
        ThinkingContext input,
        CancellationToken cancellationToken)
    {
        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow, input, checkpointManager);

        sessionManager.RegisterRun(run);

        try
        {
            ThinkingContext? result = null;

            await foreach (var evt in run.WatchStreamAsync()
                .WithCancellation(cancellationToken))
            {
                // Bridge all events to connected clients (feature 65)
                await eventBridge.PublishEventAsync(evt, cancellationToken);

                if (evt is WorkflowOutputEvent<ThinkingContext> output)
                {
                    result = output.Value;
                }
            }

            return result ?? throw new InvalidOperationException(
                "Workflow completed without producing output");
        }
        finally
        {
            sessionManager.UnregisterRun();
        }
    }
}
```

#### 5. Integration with Existing Processing Loop

The current `ProcessingLoopService` in Leontes.Api is refactored to use the workflow:

```csharp
// Before (linear)
var response = await chatAgent.InvokeAsync(messages, cancellationToken);

// After (workflow)
var context = new ThinkingContext
{
    MessageId = message.Id,
    ConversationId = conversation.Id,
    UserContent = message.Content,
    Channel = message.Channel.ToString()
};

var result = await workflowHost.ProcessAsync(context, cancellationToken);

// Persist result
message.Content = result.Response;
message.IsComplete = result.IsComplete;
await dbContext.SaveChangesAsync(cancellationToken);
```

### How Checkpointing Works

After each Executor completes, the framework creates a checkpoint containing:
- The `ThinkingContext` state at that point
- Which Executor to resume from
- Any pending `RequestPort` requests

If the server restarts:

```csharp
// On startup, check for incomplete workflows
var pendingCheckpoint = await checkpointStore.GetLatestIncompleteAsync(cancellationToken);
if (pendingCheckpoint is not null)
{
    logger.LogInformation(
        "Resuming thinking pipeline from checkpoint {CheckpointId}, " +
        "stage {LastCompletedStage}",
        pendingCheckpoint.CheckpointId,
        pendingCheckpoint.LastCompletedExecutor);

    await using var run = await InProcessExecution.ResumeStreamingAsync(
        workflow, pendingCheckpoint, checkpointManager);

    // Continue processing from where it left off
    await foreach (var evt in run.WatchStreamAsync())
    {
        await eventBridge.PublishEventAsync(evt, cancellationToken);
    }
}
```

### How Human-in-the-Loop Works

When the Plan Executor determines it needs clarification:

```
1. PlanExecutor calls context.SendMessageAsync(QuestionRequest)
2. Framework emits RequestInfoEvent with the question
3. Framework creates a checkpoint and SUSPENDS the workflow
4. IWorkflowEventBridge delivers the event to CLI via persistent SSE (feature 65)
5. CLI shows: "🤔 Clarification needed: Should I include archived projects?"
6. User types response → POST /api/v1/stream/respond { requestId, response }
7. API calls run.SendResponseAsync(response)
8. Framework RESUMES the workflow from the checkpoint
9. PlanExecutor receives the response and continues
```

This is the same `RequestPort` mechanism described in feature 65. The Thinking Pipeline is the primary consumer.

### Error Handling

Executors that are purely enrichment (Perceive, Enrich, Reflect) should degrade gracefully. Core executors (Plan, Execute) should abort on failure.

The framework wraps each Executor in try/catch and emits `ExecutorFailedEvent` on error. The Workflow Host maps this to the global exception handler:

| Scenario | Behavior |
|---|---|
| Perceive fails to extract entities | Log warning, pass context with empty entities to Enrich |
| Enrich finds no memories | Normal — pass context with history only to Plan |
| Enrich can't reach vector DB | Log error, continue with conversation history only |
| Plan LLM call fails | ExecutorFailedEvent → abort, return error to client |
| Plan asks question, user never responds | Timeout (5 min), continue with safe default |
| Execute LLM call fails | ExecutorFailedEvent → abort, return error to client |
| Execute interrupted (client disconnect) | Mark IsComplete = false, checkpoint saved, skip Reflect |
| Reflect fails to store memory | Log error, response already sent — no user impact |
| Server restarts mid-pipeline | Resume from last checkpoint, continue from next stage |

### SSE Event Stream

Events emitted by the pipeline, consumed by the IWorkflowEventBridge (feature 65):

| Source | Event Type | Example |
|---|---|---|
| Any executor | `ProgressEvent` | `{stage: "Enrich", description: "Found 5 memories", progress: 0.4}` |
| ExecuteExecutor | `AgentResponseUpdateEvent` | `{text: "Based on"}` (streaming token) |
| ReflectExecutor | `InsightEvent` | `{content: "User prefers PostgreSQL over MySQL"}` |
| PlanExecutor | `RequestInfoEvent` | `{type: "question", content: "Include archived?"}` |
| Framework | `ExecutorInvokedEvent` | `{executor: "Perceive"}` |
| Framework | `SuperStepCompletedEvent` | `{stepNumber: 2, checkpointId: "..."}` |
| Framework | `WorkflowOutputEvent` | Final ThinkingContext |

### Per-Stage Model Tier and Temperature

Each executor receives the appropriate `IChatClient` via keyed DI and applies stage-specific `ChatOptions`. Configuration is defined in feature 75 (Agent Persona & Model Configuration):

| Stage | Model Tier | Temperature | LLM Call |
|---|---|---|---|
| Perceive | — | — | No (heuristics only) |
| Enrich | — | — | No (DB queries only) |
| Plan | Large | 0.2 | Yes |
| Execute | Large | 0.5 | Yes (streaming) |
| Reflect | Small | 0.1 | Optional (insight extraction) |

Executors that call the LLM (Plan, Execute, Reflect) inject `[FromKeyedServices("Large")]` or `[FromKeyedServices("Small")]` and build per-invocation `ChatOptions` from `PersonaOptions.StageSettings`.

### Configuration

```json
{
    "ThinkingPipeline": {
        "CheckpointStorage": "PostgreSQL",
        "DefaultQuestionTimeoutMinutes": 5,
        "MaxConversationHistoryMessages": 20,
        "MaxRelevantMemories": 10,
        "MemoryRelevanceThreshold": 0.7
    }
}
```

### DI Registration

```csharp
public static IServiceCollection AddThinkingPipeline(this IServiceCollection services)
{
    // Register executors
    services.AddSingleton<PerceiveExecutor>();
    services.AddSingleton<EnrichExecutor>();
    services.AddSingleton<PlanExecutor>();
    services.AddSingleton<ExecuteExecutor>();
    services.AddSingleton<ReflectExecutor>();

    // Register workflow
    services.AddSingleton(sp => ThinkingWorkflowDefinition.Build(sp));

    // Register workflow host
    services.AddSingleton<ThinkingWorkflowHost>();

    // Register checkpoint manager
    services.AddSingleton(sp =>
    {
        var store = sp.GetRequiredService<ICheckpointStore>();
        return CheckpointManager.CreateJson(store);
    });

    return services;
}
```

## Acceptance Criteria

- [ ] Processing Loop refactored to use Agent Framework Workflow with 5 Executors
- [ ] Each Executor is independently testable with mock dependencies
- [ ] ThinkingContext is JSON-serializable for checkpointing
- [ ] Workflow survives server restart — resumes from last checkpoint
- [ ] PlanExecutor can pause the workflow to ask the user a question via RequestPort
- [ ] All Executors emit ProgressEvent at start/end for observability
- [ ] ExecuteExecutor streams response tokens as AgentResponseUpdateEvent
- [ ] ReflectExecutor stores episodic memories and graph updates (when memory system is available)
- [ ] Enrichment stages degrade gracefully — failures don't block LLM response
- [ ] Existing chat functionality is unchanged — the pipeline is transparent to the user
- [ ] SSE event bridge receives all workflow events and streams them to connected clients
- [ ] All Executors log at appropriate levels (Debug for routine, Warning for degraded, Error for failures)
- [ ] Each LLM-calling stage uses the configured model tier (Large/Small) from feature 75
- [ ] Each LLM-calling stage applies the configured temperature from feature 75
- [ ] Each stage wraps execution in resilience boundary with degraded fallback (feature 85)
- [ ] Token usage is metered per stage via ITokenMeter (feature 100)
- [ ] Each stage emits DecisionRecords for non-trivial choices (feature 95)

## Out of Scope

- Parallel executor execution (stages run serially for state consistency)
- Multi-agent coordination (single agent, per architecture spec)
- Conditional stage skipping based on message type (all messages go through all stages — no-op is cheap)
- Dynamic executor graph reconfiguration at runtime
- Workflow versioning (handled when needed for migrations)
