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

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET SDK](https://dot.net/download) | 10+ | Build and run the backend |
| [Docker](https://docs.docker.com/get-docker/) | Latest | Run PostgreSQL locally |
| [Ollama](https://ollama.com/) | Latest | Local LLM inference |

### First-time setup

```bash
# 1. Pull the AI model used for local development
ollama pull qwen2.5:7b

# 2. Create a .env file for Docker Compose (copy the example and adjust if needed)
cp .env.example .env

# 3. Set the database connection string for the API and Worker
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=leontes;Username=leontes;Password=leontes" \
  --project backend/src/Leontes.Api

dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=leontes;Username=leontes;Password=leontes" \
  --project backend/src/Leontes.Worker

# 4. Install the CLI as a dotnet tool
dotnet pack backend/src/Leontes.Cli/ --configuration Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg Leontes.Cli

# 5. Generate and configure the API key (sets User Secrets for API, Worker, and CLI)
leontes init
```

> **Reinstalling the CLI after code changes:** Uninstall first, then pack and install again:
> ```bash
> dotnet tool uninstall --global Leontes.Cli
> dotnet pack backend/src/Leontes.Cli/ --configuration Release -o ./nupkg
> dotnet tool install --global --add-source ./nupkg Leontes.Cli
> ```

> **EF Core migrations:** The design-time factory reads `LEONTES_CONNECTION_STRING` from the environment. Set it before running `dotnet ef migrations add`:
>
> ```bash
> # PowerShell
> $env:LEONTES_CONNECTION_STRING="Host=localhost;Port=5432;Database=leontes;Username=leontes;Password=leontes"
>
> # Bash
> export LEONTES_CONNECTION_STRING="Host=localhost;Port=5432;Database=leontes;Username=leontes;Password=leontes"
> ```

### Running locally

```bash
# Terminal 1 — PostgreSQL
docker compose up -d db

# Terminal 2 — API (auto-migrates the database on first run)
dotnet run --project backend/src/Leontes.Api --configuration Release

# Terminal 3 — CLI chat
leontes chat
```

Once the CLI starts, type a message and hit Enter. That's it.

The **Worker** (Sentinel + Signal bridge) is optional during development — most of its functionality is still in progress. If you want to run it:

```bash
dotnet run --project backend/src/Leontes.Worker --configuration Release  # Windows only
```

> **Note:** All `dotnet run` / `dotnet build` commands use `--configuration Release` because Windows Application Control (WDAC) blocks unsigned Debug-built DLLs. The CLI is installed as a global dotnet tool (see First-time setup) and is not affected.

> **Note:** Ollama must be running before you start the API. If you installed Ollama normally it runs in the background automatically. If not, start it with `ollama serve`.

### Build and test

```bash
dotnet build backend/ --configuration Release
dotnet test backend/ --configuration Release
```

### Health check

The API exposes a `/_health` endpoint. You can verify everything is connected:

```bash
# Local development (default launch profile)
curl http://localhost:5154/_health

# Docker Compose
curl http://localhost:5000/_health
```

### Secrets

Use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) for local development. Never put secrets in committed files.

### CI

GitHub Actions: restore, build, test on push to `main`, `develop`, `feature/*`. Must pass before merge.

## Status

Early development. MVP scope: Sentinel (file system, clipboard, calendar, active window), Structural Vision, Synapse Graph, Tool Forge (autonomous with user approval), CLI + Signal.

## License

AGPL-3.0 — free for personal use. Commercial use requires a [commercial license](mailto:leontes.dev@pm.me).
