# Leontes

<p align="center">
  <img src="docs/logo.png" alt="Leontes" width="320" />
</p>

**An AI agent that lives in your OS, acts before you ask, and writes its own tools.**

[leontes.dev](https://leontes.dev)

Leontes is a self-hosted, proactive AI agent for Windows. It monitors your system events, understands application UI structurally, remembers context across conversations through a knowledge graph, and — when it encounters something it can't do — writes a new tool, tests it, and asks you to approve it.

Talk to it from your terminal. Message it from your phone via Signal. Or don't talk to it at all — it'll notice when you need help.

## What makes this different

| Feature | What it does |
|---------|-------------|
| **Sentinel** | Watches file downloads, clipboard, calendar, and active windows. Triggers suggestions without you asking. |
| **Structural Vision** | Reads application UI via Windows UI Automation — no screenshots, no pixel matching. Sees buttons and text as code. |
| **Synapse Graph** | Knowledge graph linking people, files, and projects. "Send this to the lead dev" just works. |
| **Tool Forge** | The agent writes, compiles, tests, and registers new tools at runtime. You approve before anything runs. |
| **CLI + Signal** | Talk to it from your PC or message it from your phone. Same context, same memory. |

## Quick start

```bash
docker compose up -d db                              # Start PostgreSQL
dotnet run --project backend/src/Leontes.Api         # Start the API
leontes                                              # Start talking
```

## Architecture

Three executable projects sharing one AI engine and one knowledge graph:

| Component | Project | Responsibility |
|-----------|---------|----------------|
| **Backend API** | `Leontes.Api` | HTTP endpoints, Processing Loop, SSE streaming, auto-migration, rate limiting |
| **Worker** | `Leontes.Worker` | Windows Service: Sentinel (OS monitoring) + Signal bridge |
| **CLI** | `Leontes.Cli` | dotnet tool (`leontes`): chat, setup wizard, user commands |

```
Sentinel (FS / Clipboard / Calendar / Window)  [Worker]
  --> Pattern Match --> AI Layer --> CLI or Signal notification

Structural Vision (Windows UI Automation)
  --> Element Tree --> AI reads and interacts via accessibility API

CLI / Signal --> Processing Loop --> Synapse Graph --> LLM + Tools --> Response  [Api]
```

**Stack:** .NET 10 Minimal API, PostgreSQL 17 + pgvector, Microsoft.Agents.AI, Windows UI Automation.

## Development

### Prerequisites

- .NET 10 SDK
- Docker & Docker Compose (for PostgreSQL)
- Ollama with a local model (e.g. `qwen2.5:7b`)

### Running locally

```bash
docker compose up -d db                              # Start PostgreSQL
dotnet run --project backend/src/Leontes.Api         # Start the API (auto-migrates DB)
dotnet run --project backend/src/Leontes.Cli         # Start the CLI chat
dotnet run --project backend/src/Leontes.Worker      # Start Sentinel (Windows only, optional)
```

Build and test:

```bash
dotnet build backend/
dotnet test backend/ --configuration Release
```

The Worker runs natively on Windows (needs OS APIs for Sentinel). It does not run in Docker.

### Secrets

Use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) for local development. Never put secrets in committed files.

### CI

GitHub Actions: restore, build, test on push to `main`, `develop`, `feature/*`. Must pass before merge.

## Status

Early development. MVP scope: Sentinel (file system, clipboard, calendar, active window), Structural Vision, Synapse Graph, Tool Forge (autonomous with user approval), CLI + Signal.

## License

AGPL-3.0 — free for personal use. Commercial use requires a [commercial license](mailto:leontes.dev@pm.me).
