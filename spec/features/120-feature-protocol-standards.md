# 120 — Industry Protocol Standards

## Problem

Leontes currently exposes a custom SSE-based API (`POST /api/v1/messages`, `GET /api/v1/stream`) that only the Leontes CLI can consume. This means:

- No web dashboard or mobile app can interact with the assistant without reimplementing the full protocol
- No external agents can collaborate with Leontes (other AI assistants, company tools, automation systems)
- No external tool servers (MCP servers) can extend Leontes' capabilities without building custom integrations
- The assistant is a closed island — it can't participate in the emerging ecosystem of interoperable AI agents

Three open protocols have emerged as the industry standard for agent interoperability. The Microsoft Agent Framework supports all three natively. Leontes should adopt them to be future-proof and maximally extensible.

## Prerequisites

- Working API with Thinking Pipeline (feature 70)
- Proactive Communication with RequestPort and WorkflowEvents (feature 65)
- Tool-calling support via Microsoft.Agents.AI (feature 30)

## Rules

- All three protocols are additive — they don't replace existing CLI/Signal/Telegram communication
- Protocol endpoints are opt-in via configuration — disabled by default, enabled when needed
- AG-UI is the first priority (enables web dashboard), A2A and MCP follow
- No custom protocol implementations — use the framework's built-in packages exclusively
- Protocol endpoints share the same authentication as the existing API
- Existing CLI chat continues to work unchanged — protocols are additional access methods

## New Packages Required

Add to the approved package list:

| Package | Purpose |
|---|---|
| `Microsoft.Agents.AI.AGUI` | AG-UI client SDK (also used by CLI for standardized communication) |
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | AG-UI server hosting — `MapAGUI()` endpoint |
| `Microsoft.Agents.AI.A2A` | A2A agent-to-agent protocol SDK |
| `Microsoft.Agents.AI.Hosting.A2A` | A2A hosting support |
| `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` | A2A server hosting — `MapA2A()` endpoint |

MCP client support is built into `Microsoft.Agents.AI.Workflows` (already approved) via `DefaultMcpToolHandler`. No additional package needed for MCP tool consumption.

## Background

### The Three Agentic Protocols

The industry has converged on three complementary protocols that cover the full spectrum of agent interaction:

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│   ┌─────────┐     AG-UI      ┌──────────────────────┐      │
│   │  User   │◄══════════════►│  Leontes (Agent)     │      │
│   │  (Web,  │  streaming UI, │                      │      │
│   │  Mobile,│  HITL, state   │                      │      │
│   │  CLI)   │  sync          │                      │      │
│   └─────────┘                │                      │      │
│                              │                      │      │
│   ┌─────────┐     A2A       │                      │      │
│   │ Other   │◄══════════════►│                      │      │
│   │ Agents  │  task delegation│                      │      │
│   │ (GPT,   │  coordination  │                      │      │
│   │ Claude) │                │                      │      │
│   └─────────┘                │                      │      │
│                              │                      │      │
│   ┌─────────┐     MCP       │                      │      │
│   │ Tool    │◄══════════════►│                      │      │
│   │ Servers │  tool discovery │                      │      │
│   │ (GitHub,│  invocation    │                      │      │
│   │  Files, │                │                      │      │
│   │  DBs)   │                │                      │      │
│   └─────────┘                └──────────────────────┘      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

| Protocol | Direction | Purpose | Originated By |
|---|---|---|---|
| **AG-UI** | Agent ↔ User | Standardized streaming UI, HITL approvals, state sync, generative UI | CopilotKit (open standard) |
| **A2A** | Agent ↔ Agent | Task delegation, coordination, artifact exchange between agents | Google (open standard) |
| **MCP** | Agent ↔ Tools/Data | Tool discovery, invocation, resource access from external servers | Anthropic (open standard) |

### Why All Three Matter

**AG-UI** unlocks the ability to build a web dashboard, mobile app, or any rich UI for Leontes without reimplementing the communication layer. CopilotKit provides ready-made React components that speak AG-UI natively — a full web chat UI with streaming, tool rendering, HITL approvals, and state sync comes almost for free.

**A2A** lets Leontes collaborate with other AI agents. Example: Leontes detects a code review task (Sentinel), delegates it to a specialized code-review agent (Claude, GPT, or another Leontes instance), and integrates the result. This is the foundation for multi-agent workflows without tight coupling.

**MCP** lets Leontes consume tools from any MCP server — GitHub, filesystem, databases, APIs — without writing custom tool integrations. The ecosystem of MCP servers is growing rapidly. Instead of building a "GitHub tool" in Tool Forge, Leontes connects to the GitHub MCP server and gets 50+ tools instantly.

### How Each Protocol Maps to Leontes

| Leontes Feature | AG-UI | A2A | MCP |
|---|---|---|---|
| CLI chat | CLI becomes AG-UI client | — | — |
| Web dashboard (future) | CopilotKit + AG-UI | — | — |
| Proactive Communication (55) | RequestInfoEvent → AG-UI approval events | — | — |
| Tool Forge (100) | Tool calls rendered in UI via AG-UI | — | — |
| Thinking Pipeline (65) | Progress events streamed via AG-UI | — | — |
| External agent collaboration | — | Leontes as A2A server + client | — |
| External tool integration | — | — | MCP tool servers |
| Sentinel alerts | AG-UI notifications | — | — |
| Structural Vision (90) | UI tree shared via AG-UI state | — | — |

## Solution

### Phase 1: AG-UI — Agent ↔ User (Priority: High)

#### What It Enables

- **Web dashboard**: Any AG-UI compatible frontend (CopilotKit, custom React) connects to Leontes
- **Standardized CLI**: CLI can optionally use `AGUIChatClient` instead of raw HTTP
- **HITL in UI**: `ApprovalRequiredAIFunction` renders as native AG-UI approval events — web clients show approve/reject buttons automatically
- **State sync**: Shared state between server and client (e.g., current conversation context, pipeline stage)
- **Streaming**: All response tokens, progress events, and notifications stream via AG-UI's SSE protocol

#### Implementation

Register AG-UI alongside existing endpoints in Leontes.Api:

```csharp
// Program.cs — add AG-UI services
builder.Services.AddAGUI();

// Map AG-UI endpoint alongside existing API
app.MapAGUI("/ag-ui", agent);

// Existing endpoints remain unchanged
app.MapChatEndpoints();
app.MapStreamEndpoints();
```

The `MapAGUI()` extension handles:
- SSE streaming with AG-UI event types (`TextMessageStart`, `TextMessageContent`, `TextMessageEnd`, `ToolCallStart`, `ToolCallArgs`, `ToolCallResult`, `RunStarted`, `RunFinished`)
- `ApprovalRequiredAIFunction` → AG-UI HITL approval events (automatic)
- State snapshots via `ChatResponseFormat.ForJsonSchema<T>()`
- Session management via `AgentSession` (maps to Leontes `Conversation`)

#### AG-UI Event Mapping

| Leontes Event | AG-UI Event |
|---|---|
| `AgentResponseUpdateEvent` (streaming token) | `TextMessageContent` |
| `ProgressEvent` (pipeline stage) | `StateDelta` (custom state) |
| `NotificationEvent` (Sentinel alert) | `TextMessageContent` (in notification thread) |
| `RequestInfoEvent` (HITL question) | AG-UI approval event |
| `ToolApprovalRequestContent` | AG-UI tool approval protocol |
| `WorkflowOutputEvent` | `RunFinished` |

#### CLI Migration Path

The CLI can optionally switch from raw HTTP to `AGUIChatClient`:

```csharp
// Before: raw HTTP
var response = await httpClient.PostAsync("/api/v1/messages", content);
// Read SSE manually...

// After: AGUIChatClient (standardized)
var chatClient = new AGUIChatClient(httpClient, "http://localhost:5000/ag-ui");
var agent = chatClient.AsAIAgent(name: "leontes-cli");
var session = await agent.CreateSessionAsync();

await foreach (var update in agent.RunStreamingAsync(messages, session))
{
    // Streaming tokens, tool calls, approvals — all handled by protocol
}
```

This is optional — the raw HTTP endpoint remains for backward compatibility. But it means the CLI gets HITL approvals, state sync, and all AG-UI features for free.

### Phase 2: MCP — Agent ↔ Tools/Data (Priority: Medium)

#### What It Enables

- Connect to any MCP server to gain its tools (GitHub, filesystem, databases, Slack, etc.)
- No custom tool code needed — tools are discovered and invoked via the MCP protocol
- Tool Forge (feature 115) creates tools locally; MCP provides external tools
- Combined catalog: forged tools + MCP tools, all available to the agent

#### Implementation

MCP client support is built into `Microsoft.Agents.AI.Workflows` via `DefaultMcpToolHandler`. Leontes connects to configured MCP servers at startup and registers their tools:

```csharp
// Configuration
{
    "McpServers": [
        {
            "Name": "github",
            "Transport": "http",
            "Url": "http://localhost:3000",
            "Enabled": true
        },
        {
            "Name": "filesystem",
            "Transport": "stdio",
            "Command": "npx",
            "Args": ["-y", "@modelcontextprotocol/server-filesystem", "C:\\Users\\user\\Documents"],
            "Enabled": true
        }
    ]
}
```

```csharp
// In AddInfrastructure() — register MCP tool handler
services.AddSingleton<IMcpToolHandler, DefaultMcpToolHandler>();

// MCP tools are discovered at startup and added to the agent's tool catalog
// alongside forged tools and built-in tools
```

#### MCP + Tool Forge Interaction

The tool catalog becomes layered:

```
Tool Resolution Order:
1. Built-in tools (compiled into Infrastructure)
2. Forged tools (Roslyn-compiled, stored in DB)
3. MCP tools (discovered from configured MCP servers)

If a forged tool and MCP tool have the same name → forged tool wins (local override)
```

When the agent detects a capability gap (Tool Forge trigger), it first checks MCP servers for a matching tool before attempting to forge one. This avoids reinventing tools that already exist:

```csharp
// In ToolForgeService — check MCP before forging
var mcpTool = await mcpToolHandler.FindToolAsync(capability, cancellationToken);
if (mcpTool is not null)
{
    logger.LogInformation(
        "Found MCP tool {ToolName} on server {Server} — skipping forge",
        mcpTool.Name, mcpTool.ServerName);
    return ToolForgeResult.McpTool(mcpTool);
}

// No MCP tool found — proceed to forge
```

### Phase 3: A2A — Agent ↔ Agent (Priority: Low)

#### What It Enables

- Other AI agents (GPT, Claude, company-internal agents) can send tasks to Leontes
- Leontes can delegate sub-tasks to specialized external agents
- Multi-agent coordination without tight coupling — each agent is a service with a discoverable "Agent Card"

#### Implementation

Leontes exposes an A2A endpoint via `MapA2A()`:

```csharp
// Program.cs — add A2A endpoint
app.MapA2A("/a2a", agent);
```

The A2A Agent Card describes Leontes' capabilities to other agents:

```json
{
    "name": "Leontes",
    "description": "Proactive OS partner — monitors system events, manages knowledge graph, extends its own capabilities",
    "url": "http://localhost:5000/a2a",
    "capabilities": {
        "streaming": true,
        "pushNotifications": false
    },
    "skills": [
        {
            "id": "os-monitoring",
            "name": "OS Event Monitoring",
            "description": "Monitors file system, clipboard, calendar, and active window events"
        },
        {
            "id": "knowledge-graph",
            "name": "Knowledge Graph Query",
            "description": "Queries the Synapse Graph for entity relationships and semantic search"
        },
        {
            "id": "tool-forge",
            "name": "Dynamic Tool Creation",
            "description": "Creates new tools at runtime via Roslyn compilation"
        }
    ],
    "defaultInputModes": ["text"],
    "defaultOutputModes": ["text"]
}
```

#### A2A Task Delegation (Outbound)

Leontes can delegate tasks to external agents when it detects they'd be better suited:

```csharp
// Example: delegate code review to a specialized agent
var a2aClient = new A2AClient(externalAgentUrl);
var task = await a2aClient.SendTaskAsync(new A2ATask
{
    Message = new A2AMessage
    {
        Role = "user",
        Parts = [new TextPart("Review this code for security issues: ...")]
    }
});

// Stream results back
await foreach (var update in a2aClient.StreamTaskAsync(task.Id))
{
    // Integrate external agent's response into Leontes' context
}
```

This is the most speculative protocol — useful for future multi-agent scenarios but not critical for the core single-user experience.

### Configuration

```json
{
    "Protocols": {
        "AGUI": {
            "Enabled": true,
            "Path": "/ag-ui",
            "CorsOrigins": ["http://localhost:3000"]
        },
        "MCP": {
            "Enabled": false,
            "Servers": []
        },
        "A2A": {
            "Enabled": false,
            "Path": "/a2a",
            "AllowExternalAgents": false
        }
    }
}
```

### Security Considerations

| Protocol | Auth | Considerations |
|---|---|---|
| AG-UI | Same JWT/API key as existing API | CORS must be configured for web dashboard origin |
| MCP | Per-server auth (API keys, OAuth) | MCP servers may have their own auth — configured per server |
| A2A | Agent Card validation + API key | Inbound A2A requests must be authenticated — no anonymous agent access |

- AG-UI endpoints share the same auth middleware as existing API endpoints
- MCP server credentials stored in .NET User Secrets (never in appsettings.json)
- A2A: inbound requests require API key; outbound requests use the external agent's auth
- All protocol endpoints are disabled by default — explicitly opt-in via configuration

### Architecture: How Protocols Layer on Top

```
┌─────────────────────────────────────────────────────────────┐
│                        Leontes.Api                          │
│                                                             │
│  Existing:              Protocol Endpoints:                 │
│  POST /api/v1/messages  GET/POST /ag-ui (AG-UI)             │
│  GET  /api/v1/stream    POST /a2a (A2A)                     │
│  POST /api/v1/stream/   (MCP is outbound only — no         │
│       respond            endpoint needed)                   │
│                                                             │
│  All endpoints share:                                       │
│  ├── Authentication middleware                              │
│  ├── Global exception handler (ProblemDetails)              │
│  ├── Rate limiting                                          │
│  └── CORS                                                   │
│                                                             │
│  All endpoints feed into:                                   │
│  └── Thinking Pipeline (Workflow) → same executors,         │
│      same memory, same tools, same Synapse Graph            │
│                                                             │
│  Tool resolution:                                           │
│  └── Built-in → Forged (DB) → MCP (external servers)       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

The key insight: **all three protocols converge on the same Thinking Pipeline**. Whether a request comes from CLI (raw HTTP), a web dashboard (AG-UI), or another agent (A2A), it flows through the same Perceive → Enrich → Plan → Execute → Reflect workflow. The protocol layer is just the transport — the intelligence is in the pipeline.

## Acceptance Criteria

### Phase 1: AG-UI
- [ ] `MapAGUI()` endpoint registered at `/ag-ui` in Leontes.Api
- [ ] AG-UI SSE streaming works with standard AG-UI events (TextMessage, ToolCall, Run lifecycle)
- [ ] `ApprovalRequiredAIFunction` automatically maps to AG-UI HITL approval events
- [ ] A CopilotKit React frontend can connect and chat with Leontes via AG-UI
- [ ] CLI can optionally use `AGUIChatClient` for standardized communication
- [ ] CORS configured for web dashboard origins
- [ ] AG-UI endpoint shares existing authentication

### Phase 2: MCP
- [ ] MCP servers configured via `McpServers` configuration section
- [ ] `DefaultMcpToolHandler` discovers tools from configured MCP servers at startup
- [ ] MCP tools appear in the agent's tool catalog alongside built-in and forged tools
- [ ] Tool Forge checks MCP servers before attempting to forge a new tool
- [ ] MCP server credentials stored in User Secrets
- [ ] MCP server connections resilient to server restart (reconnect with backoff)

### Phase 3: A2A
- [ ] `MapA2A()` endpoint registered at `/a2a` in Leontes.Api
- [ ] Agent Card accurately describes Leontes' capabilities
- [ ] Inbound A2A tasks are processed through the Thinking Pipeline
- [ ] Outbound A2A delegation sends tasks to configured external agents
- [ ] A2A endpoint requires authentication

## Out of Scope

- Custom AG-UI frontend (use CopilotKit or build separately — not part of backend spec)
- MCP server implementation (Leontes is an MCP client, not a server — it consumes tools, doesn't expose them via MCP)
- A2A agent orchestration framework (simple point-to-point delegation, not a full orchestrator)
- Protocol version negotiation (use the versions shipped with the framework packages)
- AG-UI Generative UI (custom React components rendered by tool calls — deferred until web dashboard is built)
- WebSocket transport for AG-UI (SSE is the standard transport)
