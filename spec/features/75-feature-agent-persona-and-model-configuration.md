# 75 — Agent Persona & Model Configuration

## Problem

The agent's personality is a single hardcoded string in `DependencyInjection.cs`: "You are Leontes, a helpful personal AI assistant. Be concise and accurate." This gives the LLM no guidance on tone, proactivity, channel-aware behavior, or boundaries. Every LLM call also uses the same model and temperature, regardless of whether it's planning a multi-step task (needs precision) or generating a conversational response (needs warmth).

Without configurable personality and per-stage model settings, the agent feels generic, and there's no way to tune its behavior without code changes.

## Prerequisites

- Working CLI chat (feature 10)
- Proactive Communication (feature 65) — keyed DI and persona are consumed by the Thinking Pipeline (feature 70), which is implemented after this feature

## Rules

- The persona is defined in a Markdown file (`persona.md`) loaded at startup — not hardcoded in code
- Per-stage model tier (Large/Small) and temperature are configured, not hardcoded
- Two named `IChatClient` instances registered via keyed DI — one Large, one Small
- The stage determines the model tier, not the query content — no "intelligent" routing
- Temperature, model tier, and persona are configurable without code changes
- Persona instructions are passed via `ChatClientAgent`'s `instructions` parameter
- Per-stage `ChatOptions` (temperature, max tokens) are passed via `ChatClientAgentRunOptions`
- Channel-aware behavior (CLI gets rich formatting, Signal/Telegram get shorter messages) is part of the persona instructions, not branching logic

## Background

### Why Not Intelligent Model Routing

Intelligent routing (classify the query first, then pick a model) adds an LLM call per request just to decide which model to use. The pipeline stage already encodes the complexity: Plan and Execute need a capable model; Reflect and Consolidation are summarization tasks. Static per-stage assignment gives the same result without the overhead.

The only runtime adjustment is budget-driven: when the budget is stressed (feature 100), the Model Router can downgrade Large → Small. This is a policy decision, not a classification task.

### Why Per-Stage Temperature

Different cognitive stages have different requirements:

| Stage | Temperature | Rationale |
|---|---|---|
| Plan | 0.2 | Deterministic reasoning, consistent tool selection |
| Execute | 0.5 | Natural conversational responses, some creativity |
| Reflect | 0.1 | Factual extraction, minimal hallucination |
| Memory Consolidation | 0.1 | Summarization accuracy |
| Sentinel Classification | 0.2 | Consistent categorization |

### The Persona File Pattern

OpenClaw uses `SOUL.md` — a Markdown file defining agent identity. This pattern is framework-agnostic and easy to iterate on. For Leontes, the persona file defines who the agent is, how it behaves, and what it should never do.

## Solution

### 1. Persona File (Project Root or Configurable Path)

`persona.md` — loaded once at startup, injected as system instructions:

```markdown
# Identity

You are Leontes, a proactive personal AI assistant running on the user's Windows machine. You have access to their file system, clipboard, calendar, and active applications through the Sentinel monitoring system. You communicate via CLI, Signal, and Telegram.

# Behavior

- Be concise. Prefer short, actionable responses over lengthy explanations.
- When you're confident (above your confidence threshold), act. When uncertain, ask.
- Proactively surface relevant information when you notice something useful — don't wait to be asked.
- If a task requires multiple steps, briefly state your plan before executing.
- Adapt your format to the channel: CLI supports rich markdown and code blocks; Signal and Telegram should be shorter and plain-text friendly.

# Boundaries

- Never execute code, modify files, or take actions with side effects without explicit user approval.
- Never share user data with external services unless the user has explicitly configured it.
- When you don't know something, say so. Don't fabricate.
- If a tool call fails, explain what happened and suggest alternatives.

# Tone

- Professional but approachable. Not robotic, not overly casual.
- Match the user's energy — if they're terse, be terse. If they're detailed, be detailed.
- Never apologize unnecessarily. Skip "I'm sorry" unless you actually made an error.
```

### 2. Model Tier Configuration

Two model tiers, registered as keyed `IChatClient` services:

```json
{
  "AiProvider": {
    "Models": {
      "Large": {
        "Provider": "ollama",
        "ModelId": "qwen2.5:7b"
      },
      "Small": {
        "Provider": "ollama",
        "ModelId": "qwen2.5:3b"
      }
    }
  }
}
```

For local development with a single model, both tiers can point to the same model. For cloud production, this maps to e.g. a flagship model (Large) vs a lightweight model (Small).

### 3. Per-Stage Configuration

```json
{
  "Persona": {
    "InstructionsFile": "persona.md",
    "ConfidenceThreshold": 0.7,
    "ProactivityLevel": "Balanced",
    "StageSettings": {
      "Plan": { "ModelTier": "Large", "Temperature": 0.2 },
      "Execute": { "ModelTier": "Large", "Temperature": 0.5 },
      "Reflect": { "ModelTier": "Small", "Temperature": 0.1 },
      "Consolidation": { "ModelTier": "Small", "Temperature": 0.1 },
      "SentinelClassification": { "ModelTier": "Small", "Temperature": 0.2 }
    }
  }
}
```

### 4. DI Registration

```csharp
private static void AddAiServices(IServiceCollection services, IConfiguration configuration)
{
    var modelsSection = configuration.GetSection("AiProvider:Models");

    // Register keyed IChatClient instances
    services.AddKeyedSingleton<IChatClient>("Large", (_, _) =>
        CreateChatClient(modelsSection.GetSection("Large")));

    services.AddKeyedSingleton<IChatClient>("Small", (_, _) =>
        CreateChatClient(modelsSection.GetSection("Small")));

    // Default (unkeyed) resolves to Large for backwards compatibility
    services.AddSingleton<IChatClient>(sp =>
        sp.GetRequiredKeyedService<IChatClient>("Large"));

    // Load persona instructions
    var personaFile = configuration["Persona:InstructionsFile"] ?? "persona.md";
    var instructions = File.ReadAllText(personaFile);

    services.AddSingleton<AIAgent>(sp =>
        new ChatClientAgent(
            sp.GetRequiredKeyedService<IChatClient>("Large"),
            instructions: instructions,
            name: "Leontes",
            tools: [AIFunctionFactory.Create(CurrentDateTimeTool.GetCurrentDateTime)],
            loggerFactory: sp.GetService<ILoggerFactory>()));
}
```

### 5. Pipeline Stage Integration

Each executor receives the appropriate `IChatClient` via keyed DI and builds stage-specific `ChatOptions`:

```csharp
public sealed class PlanExecutor(
    [FromKeyedServices("Large")] IChatClient chatClient,
    IOptions<PersonaOptions> personaOptions,
    ILogger<PlanExecutor> logger) : Executor<ThinkingContext, ThinkingContext>
{
    protected override async Task<ThinkingContext> ExecuteAsync(
        ThinkingContext input,
        ExecutorContext context,
        CancellationToken cancellationToken)
    {
        var stageSettings = personaOptions.Value.StageSettings["Plan"];

        var options = new ChatOptions
        {
            Temperature = stageSettings.Temperature,
        };

        var response = await chatClient.GetResponseAsync(
            messages, options, cancellationToken);

        // ...
    }
}
```

### 6. ProactivityLevel Enum

Controls how aggressively the Sentinel escalates events and how readily the agent volunteers information:

```csharp
public enum ProactivityLevel
{
    Minimal,    // Only respond when asked. Sentinel rarely escalates.
    Balanced,   // Surface relevant info proactively. Sentinel escalates surprises.
    Proactive   // Actively suggest actions. Sentinel escalates aggressively.
}
```

This feeds into Sentinel's escalation threshold (feature 90) and the Plan executor's decision to volunteer unsolicited context.

### Configuration Classes

```csharp
public sealed class PersonaOptions
{
    public const string SectionName = "Persona";

    public string InstructionsFile { get; set; } = "persona.md";
    public double ConfidenceThreshold { get; set; } = 0.7;
    public ProactivityLevel ProactivityLevel { get; set; } = ProactivityLevel.Balanced;
    public Dictionary<string, StageSettings> StageSettings { get; set; } = new();
}

public sealed class StageSettings
{
    public string ModelTier { get; set; } = "Large";
    public float Temperature { get; set; } = 0.5f;
}

public sealed class AiProviderOptions
{
    public const string SectionName = "AiProvider";

    public Dictionary<string, ModelOptions> Models { get; set; } = new();
}

public sealed class ModelOptions
{
    public string Provider { get; set; } = "ollama";
    public string ModelId { get; set; } = "qwen2.5:7b";
    public string? Endpoint { get; set; }
}
```

## Acceptance Criteria

- [ ] Persona instructions loaded from `persona.md` file at startup
- [ ] Two `IChatClient` instances registered via keyed DI (Large, Small)
- [ ] Each pipeline stage uses the configured model tier and temperature
- [ ] `PersonaOptions` and `AiProviderOptions` bound from configuration
- [ ] Changing `persona.md` takes effect on next startup without code changes
- [ ] Changing model tier or temperature in config takes effect on next startup
- [ ] Default persona file ships with the project and covers identity, behavior, boundaries, and tone
- [ ] ProactivityLevel is configurable and influences Sentinel escalation threshold
- [ ] ConfidenceThreshold is configurable and influences when the agent asks vs. proceeds
- [ ] CLI `leontes init` step added for AI provider model configuration (Large + Small)

## Out of Scope

- Runtime persona editing (restart required)
- Per-channel persona variations (handled by instructions in persona.md, not code branching)
- Intelligent model routing based on query classification
- User-facing persona customization UI
- Multiple persona profiles (one persona, one agent)
