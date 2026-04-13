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

### Signal setup

Signal lets you message Leontes from your phone via E2E encrypted messaging. It uses [signal-cli-rest-api](https://github.com/bbernhard/signal-cli-rest-api) running in Docker — no Java needed on your machine.

#### 1. Start the Signal container

```bash
docker compose up -d signal
```

This starts `signal-cli-rest-api` on port 8081.

#### 2. Register a phone number

You need a real phone number (prepaid SIM or VoIP) that can receive SMS.

```bash
# Request SMS verification
curl -X POST http://localhost:8081/v1/register/+YOUR_PHONE_NUMBER

# If a CAPTCHA is required, get a token from https://signalcaptchas.org/registration/generate
# and pass it in the request body:
curl -X POST http://localhost:8081/v1/register/+YOUR_PHONE_NUMBER \
  -H "Content-Type: application/json" \
  -d '{"captcha": "signal-recaptcha-v2.YOUR_TOKEN"}'
```

#### 3. Verify registration

After receiving the SMS code:

```bash
curl -X POST http://localhost:8081/v1/register/+YOUR_PHONE_NUMBER/verify/CODE
```

#### 4. Configure Leontes Worker

```bash
# Set the registered phone number
dotnet user-secrets set "Signal:PhoneNumber" "+YOUR_PHONE_NUMBER" \
  --project backend/src/Leontes.Worker

# Allow your personal phone to send messages to Leontes
dotnet user-secrets set "Signal:AllowedSenders:0" "+YOUR_PERSONAL_NUMBER" \
  --project backend/src/Leontes.Worker
```

#### 5. Start the Worker

```bash
dotnet run --project backend/src/Leontes.Worker --configuration Release
```

The Worker will connect to signal-cli-rest-api and start polling for messages. Send a message from your phone to the registered number — Leontes will respond.

#### Troubleshooting

| Problem | Fix |
|---------|-----|
| Worker logs "Signal REST API not available" | Check `docker compose ps signal` — container must be running on port 8081 |
| Registration fails with 403 | CAPTCHA required — see step 2 above |
| Messages ignored silently | Sender not in `AllowedSenders` — check Worker logs for "unknown sender" warning |
| Worker logs "ApiKey not configured" | Run `leontes init` to generate and set the API key |
| Port 8081 already in use | Change the port mapping in `docker-compose.yml` and update `Signal:BaseUrl` in Worker user secrets |

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
