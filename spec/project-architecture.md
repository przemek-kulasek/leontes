# Project Architecture

## System Overview

Two layers running on the user's PC, sharing one AI engine and one knowledge graph:

1. **Proactive Layer** — Sentinel monitors OS events, Structural Vision reads application UI. Triggers suggestions or actions through the AI layer.
2. **Conversational Layer** — CLI and Signal feed into an async processing loop that resolves context from the knowledge graph and calls the LLM.

## Components

| Component | Tech | Responsibility |
|-----------|------|----------------|
| Backend API | .NET 10 Minimal API | Endpoints, auth, SSE streaming |
| Processing Loop | Background service | Message intake → context → LLM → response |
| Sentinel | Background service | FS watcher, clipboard, calendar, active window → pattern match → trigger |
| Structural Vision | Windows UI Automation | Read/interact with application UI as element tree |
| Tool Forge | Code generation + Roslyn | Agent writes tool classes → compile → test → register |
| CLI | Terminal client | PC interaction |
| Signal | Signal Bot / Bridge | Mobile interaction |
| Knowledge Graph | PostgreSQL 17 + pgvector | Entities, relationships, semantic search, tool usage tracking |
| AI Layer | Microsoft.Agents.AI | LLM orchestration, tool dispatch |

## Data Flow

```
Sentinel:
  OS Events → Pattern Match → AI Layer (if needed) → CLI/Signal notification

Structural Vision:
  UI Automation → Element Tree → AI reads/interacts via API

Channels:
  CLI / Signal → Queue → Processing Loop → Synapse Graph
                                          → LLM + Tools
                                          → Response → Original Channel

Tool Forge:
  Gap detected → Generate tool class → Compile + test → User approval → Register
```

## Setup

`leontes init` — interactive CLI wizard:
1. PostgreSQL via Docker Compose (or existing connection string)
2. AI provider + model + API key → .NET User Secrets
3. Signal bot registration (guided steps)
4. Sentinel defaults (watch folders, enabled inputs)
5. JWT secret + API key auto-generated

## Auth

Single-user. API key or JWT for CLI. Signal via bot registration.

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| PostgreSQL for graph + vectors | One database for entities, relationships, pgvector search |
| Windows UI Automation over screenshots | Structural, fast, cheap — no vision API calls |
| Signal for mobile | E2E encrypted, no custom mobile app needed |
| Tool Forge with user approval | Self-extending but safe — no unreviewed code runs |
| CLI wizard over web setup | Runs once, no UI to build or maintain |
| No sandbox for MVP | User confirms before execution; Vault deferred |

## Infrastructure

- **Dev:** Docker Compose (PostgreSQL, backend with hot-reload)
- **CI:** GitHub Actions (restore → build → test)
- **Prod:** TBD
