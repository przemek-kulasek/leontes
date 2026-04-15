# Project Description

## Vision

Leontes is a **Proactive OS Partner** with a neuroscience-inspired cognitive architecture. It integrates into Windows as an ambient layer that thinks in stages (perceive → enrich → plan → execute → reflect), monitors system events through dual-process filtering, builds a hierarchical knowledge graph, and extends its own capabilities by writing new tools at runtime. Reachable via CLI, Signal, Telegram, or any AG-UI compatible frontend.

## Core Problem

Modern AI agents have memory, support multiple interfaces, and can be self-hosted — but they are still **reactive**. They wait for you to ask. They also can't extend themselves: when they hit a capability gap, they stop or do it poorly. And when they do respond, they can't explain why they chose that answer, how confident they are, or what they considered and rejected.

Leontes closes all three gaps — an agent that **acts before you ask**, **writes its own tools autonomously**, and **shows its work** — while being fully transparent, self-hosted, and open-source.

## Architecture: How Leontes Thinks

### The Cognitive Pipeline (Feature 70)

Every interaction flows through a 5-stage pipeline inspired by Global Workspace Theory (Dehaene) and Dual-Process Theory (Kahneman). Built on Microsoft Agent Framework Workflows (Executor + Edge + CheckpointManager):

1. **Perceive** — Extract entities, classify intent, detect urgency. No LLM — fast pattern matching only.
2. **Enrich** — Search hierarchical memory (episodic + semantic), resolve entities via Synapse Graph, retrieve relevant context.
3. **Plan** — LLM generates a plan: which tools to call, what context to include. Can pause here to ask the user a question.
4. **Execute** — LLM produces the response (streaming tokens via SSE). Tools are called as needed.
5. **Reflect** — Store new memories, update the knowledge graph, extract insights. The agent learns from every interaction.

Each stage checkpoints its state. If the server crashes mid-pipeline, it resumes from the last completed stage.

### Dual-Process Intelligence (Features 90 + 70)

- **System 1 (Sentinel):** Fast, local, free. Monitors OS events (file downloads, clipboard, calendar, active windows) and applies heuristic filters — regex, frequency analysis, time rules. No LLM calls. Only genuinely surprising events escalate to System 2.
- **System 2 (Thinking Pipeline):** Slow, deliberate, expensive. The full 5-stage cognitive pipeline with LLM reasoning. Only triggered when System 1 can't handle it alone.

This mirrors Kahneman's model: most OS events are handled by reflexes (System 1). The "conscious mind" (System 2) only activates when something unexpected happens.

### Hierarchical Memory (Feature 80)

Four memory types modeled on neuroscience, all in PostgreSQL:

| Type | Biological Equivalent | Implementation |
|---|---|---|
| **Working Memory** | Prefrontal Cortex | Current context window (last N turns) |
| **Episodic Memory** | Hippocampus | pgvector embeddings of past experiences |
| **Semantic Memory** | Temporal Lobe | Synapse Graph (entities + relationships via recursive CTEs) |
| **Procedural Memory** | Cerebellum | Tool Forge catalog (learned skills) |

Graph-Augmented Retrieval (GraphRAG): "Send this to Sarah" → graph lookup finds Sarah → email → recent files linked to her. Not flat vector search — relationship-aware retrieval.

## Core Modules

### M1: Proactive Communication — Feature 65
Bidirectional: the agent can send notifications, ask mid-task questions, request permissions, and stream progress updates. Built on Agent Framework `RequestPort` (typed HITL channels) and `WorkflowEvent` (progress streaming). Pending requests survive server restarts via `CheckpointManager`.

### M2: Channels — Features 10, 50, 60
- **CLI** — terminal on the host machine (SSE streaming)
- **Signal** — E2E encrypted via signal-cli-rest-api
- **Telegram** — official Bot API over HTTPS
- **AG-UI** — industry-standard protocol for web frontends (CopilotKit compatible)

All channels share one `IMessagingClient` abstraction and feed into the same Thinking Pipeline.

### M3: Synapse Graph (Knowledge Graph) — Feature 80
PostgreSQL + pgvector linking People, Files, and Projects. Resolves contextual references: "send this to the lead dev" → person lookup from Git/email history. Recursive CTEs for graph traversal. Vector embeddings for semantic search.

### M4: The Sentinel (Proactive Engine) — Feature 90
Monitors OS events without user input: file system, clipboard, calendar, active window. Heuristic filters (System 1) classify and rate-limit events locally. Only surprising events escalate to the LLM (System 2). Delivered via CLI, Signal, or Telegram — channel selection is automatic.

### M5: Observability & Confidence — Feature 95
Every pipeline execution produces a trace: per-stage timing, decision records (what was considered, what was chosen, why), and a confidence score (0–1). The agent signals uncertainty — asks for clarification when confidence is low, proceeds confidently when high. Users can ask "Why did you do that?" and get a trace-based explanation.

### M6: Structural Vision — Feature 105
Windows UI Automation to read application UI as a structured element tree. The agent sees buttons, text fields, and menus as code — not pixels. Privacy exclusions prevent reading password fields or excluded apps.

### M7: Tool Forge (Self-Extending Agent) — Feature 115
The agent writes, compiles (Roslyn), tests, and registers new tools at runtime. User approval required via `ApprovalRequiredAIFunction`. Tools run in a restricted namespace sandbox. Usage tracked in Synapse Graph; unused tools pruned automatically.

### M8: Agent Persona & Model Configuration — Feature 75

Agent personality defined in `persona.md` — identity, tone, boundaries, channel-aware behavior. Two model tiers (Large/Small) registered via keyed DI. Per-stage temperature: Plan 0.2, Execute 0.5, Reflect 0.1. Proactivity level and confidence threshold configurable. Budget-driven model downgrading (Large→Small) when under pressure. Token tracking via `ChatResponse.Usage` from Microsoft.Extensions.AI + `OpenTelemetryAgent` for observability.

### M9: Privacy & Data Governance — Feature 110

All monitoring is opt-in. Users can review, search, and delete any stored data. Topic-based purge ("forget Project X") cascades across all tables. Data export in JSON + Markdown. Sensitive data (credit cards, tokens, IBANs) is never stored in plaintext. Privacy pause mode stops all monitoring instantly.

### M10: Cost Control & Budgets — Feature 100

Token metering on every LLM call via `ChatResponse.Usage`. Daily budgets with per-feature allocations (Chat 60%, Sentinel 15%, Consolidation 15%, Tool Forge 10%). Three-tier throttling: normal → warning → throttled. Budget-driven model tier downgrading. Background tasks are throttled first; interactive chat is never silently blocked.

### M11: Error Recovery & Resilience — Feature 85

Bounded processing queues with backpressure. Stage-level degradation (memory unavailable → continue with history only). LLM retry with exponential backoff. Context window overflow detection with automatic summarization. Offline mode: Sentinel heuristics, existing tools, and memory retrieval work without the LLM.

### M12: Industry Protocol Standards — Feature 120

Three agentic protocols, all via Microsoft Agent Framework:
- **AG-UI** (Agent↔User) — SSE-based, CopilotKit-compatible web frontends
- **MCP** (Agent↔Tools) — connect to external tool servers (GitHub, filesystem, databases)
- **A2A** (Agent↔Agent) — delegate tasks to and receive tasks from other AI agents

### M13: Setup Wizard — Feature 30
Interactive CLI (`leontes init`) for first-run configuration: spins up PostgreSQL via Docker Compose, configures AI provider + API keys (stored in .NET User Secrets), walks through Signal/Telegram setup, sets Sentinel preferences, generates auth secrets, requests privacy consent.

## Post-MVP

Ghost Overlay (transparent system overlay), Voice I/O, Web Dashboard (via AG-UI), The Vault (sandboxed execution in Docker/Micro-VM), AG-UI Generative UI.

## Licensing & Monetization

**License:** AGPL-3.0 with commercial dual-licensing.

- **Free** — personal use, experimentation, research, non-commercial. Full-featured, no warranty.
- **Paid (commercial license)** — any company using Leontes to support its business, bundling it in a product, or redistributing it. Per-seat or per-organization pricing.

Source code is fully public. AGPL enforces this split: commercial users who don't want to open-source their own stack must buy the commercial license.

## Scope Boundaries

- **MVP:** Cognitive Pipeline, Agent Persona & Model Config, Hierarchical Memory, Sentinel (all 4 inputs), Structural Vision, Synapse Graph, CLI + Signal + Telegram, Tool Forge, Proactive Communication, Observability, Privacy Controls, Cost Management, Error Recovery, Industry Protocol Standards (AG-UI, MCP, A2A), Setup Wizard.
- **Post-MVP:** Ghost Overlay, voice, web dashboard, Vault (sandboxed execution), AG-UI Generative UI.
- **Out of scope:** Multi-user, macOS/Linux Structural Vision, multi-provider load balancing.
