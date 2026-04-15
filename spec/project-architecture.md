# Project Architecture

## System Overview

Three executable projects running on the user's PC, sharing one AI engine and one knowledge graph:

1. **Leontes.Api** — HTTP endpoints + Processing Loop (`IHostedService`). The brain. Handles chat requests, runs the LLM, manages the Synapse Graph. Includes rate limiting, CORS, auto-migration on startup.
2. **Leontes.Worker** — Windows Service running the Proactive Layer. Sentinel monitors OS events, messaging bridges (Signal, Telegram) receive mobile messages and forward them to the API.
3. **Leontes.Cli** — dotnet tool (`leontes`). The user interface. Setup wizard (`leontes init`), interactive chat, and future commands.

## Components

| Component | Project | Tech | Responsibility |
|-----------|---------|------|----------------|
| Backend API | Leontes.Api | .NET 10 Minimal API | Endpoints, auth, SSE streaming, rate limiting, CORS |
| Processing Loop | Leontes.Api | IHostedService | Message intake → context → LLM → response |
| Sentinel | Leontes.Worker | Windows Service | FS watcher, clipboard, calendar, active window → pattern match → trigger |
| Signal Bridge | Leontes.Worker | Windows Service | Receives Signal messages, forwards to API |
| Telegram Bridge | Leontes.Worker | Windows Service | Receives Telegram messages, forwards to API |
| Structural Vision | Leontes.Worker | Windows UI Automation | Read/interact with application UI as element tree |
| CLI | Leontes.Cli | dotnet tool | PC interaction — chat, setup wizard |
| Knowledge Graph | Shared (Infrastructure) | PostgreSQL 17 + pgvector | Entities, relationships, semantic search, tool usage tracking |
| AI Layer | Shared (Infrastructure) | Microsoft.Agents.AI | LLM orchestration, tool dispatch, two model tiers (Large/Small) via keyed DI |
| Agent Persona | Shared (Configuration) | persona.md + PersonaOptions | Identity, behavior, boundaries, per-stage temperature |
| Token Metering | Shared (Infrastructure) | M.E.AI UsageDetails + OpenTelemetryAgent | Per-call token tracking, budget enforcement |
| Tool Forge | Shared (Infrastructure) | Roslyn + code gen | Agent writes tool classes → compile → test → register |

## Data Flow

```
Sentinel (Worker):
  OS Events → Pattern Match → AI Layer (if needed) → CLI/Signal notification

Structural Vision (Worker):
  UI Automation → Element Tree → AI reads/interacts via API

Channels:
  CLI → HTTP → Api → Processing Loop → Synapse Graph
                                       → LLM + Tools
                                       → SSE Response → CLI

  Signal   → Worker → HTTP → Api → Processing Loop → Response → Worker → Signal
  Telegram → Worker → HTTP → Api → Processing Loop → Response → Worker → Telegram

Tool Forge:
  Gap detected → Generate tool class → Compile + test → User approval → Register
```

## Setup

`leontes init` — interactive CLI wizard (implemented in Leontes.Cli):
1. PostgreSQL via Docker Compose (or existing connection string)
2. AI provider + model + API key → .NET User Secrets
3. Signal bot registration (guided steps)
3b. Telegram bot setup (BotFather token + chat ID)
4. Sentinel defaults (watch folders, enabled inputs)
5. JWT secret + API key auto-generated

## Auth

Single-user. API key or JWT for CLI ↔ Api. Signal via bot registration.

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| Two hosts (Api + Worker) + CLI tool | Api and Processing Loop are tightly coupled (SSE streaming). Sentinel needs Windows APIs (separate process). CLI is a user-facing tool. |
| Processing Loop in Api, not Worker | Processing Loop handles HTTP requests from CLI and returns SSE streams — must live where Kestrel lives |
| Worker as Windows Service | Sentinel needs OS-level access (file system, clipboard, active window). Must persist across user sessions. |
| CLI as dotnet tool | Installed globally, invoked as `leontes` from anywhere. No backend project references — HTTP only. |
| PostgreSQL for graph + vectors | One database for entities, relationships, pgvector search. Mature .NET/EF Core support via Pgvector.EntityFrameworkCore. SQLite evaluated and rejected — no NuGet package for sqlite-vec, no EF Core vector integration, pre-1.0 maturity. |
| Two model tiers (Large/Small) | Static per-stage assignment via keyed DI. Plan + Execute → Large; Reflect + Consolidation + Sentinel → Small. No intelligent routing — stage determines tier. Budget-driven downgrading (Large→Small) when budget is stressed. |
| Persona file (persona.md) | Agent personality, tone, boundaries defined in Markdown. Loaded at startup as system instructions. Per-stage temperature configured separately. |
| Token tracking via M.E.AI | ChatResponse.Usage provides InputTokenCount/OutputTokenCount. OpenTelemetryAgent wrapper for observability. Custom ITokenMeter decorator for budget enforcement. |
| Windows UI Automation over screenshots | Structural, fast, cheap — no vision API calls |
| Signal for mobile | E2E encrypted, no custom mobile app needed |
| Telegram for mobile | Official Bot API, no SIM card needed, easier setup |
| Tool Forge with user approval | Self-extending but safe — no unreviewed code runs |
| CLI wizard over web setup | Runs once, no UI to build or maintain |
| No sandbox for MVP | User confirms before execution; Vault deferred |

## Infrastructure

- **Dev:** Docker Compose (PostgreSQL + Api with hot-reload). Worker runs natively on Windows.
- **CI:** GitHub Actions (restore → build → test) — all projects including Worker, Cli, and tests
- **Prod:** TBD
