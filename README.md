# Leontes

<p align="center">
  <img src="docs/logo.png" alt="Leontes" width="320" />
</p>

**A self-hosted AI agent that thinks in stages, acts before you ask, and writes its own tools.**

[leontes.dev](https://leontes.dev)

Leontes is a proactive AI agent for Windows with a neuroscience-inspired cognitive architecture. It doesn't just respond to what you type — it monitors your system, remembers what matters, and extends its own capabilities at runtime.

Talk to it from your terminal. Message it from your phone via Signal or Telegram. Or don't talk to it at all — it'll notice when you need help.

## How it thinks

Most AI agents are a `while(true)` loop around a chat API. Leontes runs a 5-stage cognitive pipeline modeled on Global Workspace Theory and Kahneman's dual-process model:

```
Perceive ──► Enrich ──► Plan ──► Execute ──► Reflect
   │            │          │         │           │
entities    memories    strategy   response    learning
 + intent   + graph     + tools    + streaming  + graph updates
```

Each stage is an independent executor. The pipeline checkpoints after every stage — if the server crashes, it resumes from where it left off. The agent can pause mid-pipeline to ask you a question and continue when you answer.

### System 1 + System 2

Not everything goes through the full pipeline. Leontes uses a dual-process architecture:

- **System 1 (Sentinel):** Fast, local, free. Watches your file downloads, clipboard, calendar, and active windows. Applies heuristic filters — regex, frequency analysis, time rules. No LLM calls. Handles most OS events by reflex.
- **System 2 (Thinking Pipeline):** Slow, deliberate, powerful. The full 5-stage pipeline with LLM reasoning. Only activated when System 1 detects something it can't handle alone.

The result: your agent notices when you copy an IBAN and asks if you want to find the matching invoice — without burning tokens on every clipboard change.

## What makes this different

| Capability | How |
|---|---|
| **Cognitive Pipeline** | 5-stage thinking process (Perceive → Enrich → Plan → Execute → Reflect) with checkpoint recovery and mid-task human interaction |
| **Hierarchical Memory** | 4 memory types: Working (context), Episodic (past events via pgvector), Semantic (knowledge graph), Procedural (learned skills) |
| **Proactive Intelligence** | Dual-process Sentinel — local heuristics filter OS events, only surprising ones reach the LLM |
| **Structural Vision** | Reads application UI via Windows UI Automation — sees buttons and text as code, not pixels |
| **Self-Extending** | Writes, compiles, tests, and registers new tools at runtime via Roslyn. You approve before anything runs |
| **Confidence Scoring** | Signals how certain it is (0–1). Asks for clarification when uncertain, proceeds confidently when sure |
| **Show Your Work** | Every decision is traced. Ask "Why did you do that?" and get a real answer from stored pipeline traces |
| **Cost Aware** | Token budgets per feature, automatic model routing (small model for simple tasks, large for complex), throttling before you hit limits |
| **Privacy First** | All monitoring is opt-in. Review, export, or delete any stored data. "Forget Project X" cascades across all tables |
| **Multi-Channel** | CLI + Signal (E2E encrypted) + Telegram. Same brain, same memory, any device |
| **Protocol Standards** | AG-UI (web frontends), MCP (external tool servers), A2A (agent-to-agent) — all via Microsoft Agent Framework |

## Architecture

Three executable projects sharing one cognitive engine and one knowledge graph:

| Component | Project | Responsibility |
|-----------|---------|----------------|
| **Backend API** | `Leontes.Api` | Thinking Pipeline, HTTP endpoints, SSE streaming, auto-migration, rate limiting |
| **Worker** | `Leontes.Worker` | Windows Service: Sentinel (OS monitoring) + Signal/Telegram bridges |
| **CLI** | `Leontes.Cli` | dotnet tool (`leontes`): chat, setup wizard, privacy controls, budget dashboard |

```
                         ┌─────────────────────────┐
                         │     Thinking Pipeline    │
                         │  Perceive → Enrich →     │
  CLI ──────────────►    │  Plan → Execute → Reflect│    ──► Response
  Signal ───────────►    │         ▲         │      │
  Telegram ─────────►    │    Memory +    Tools +   │
  Sentinel ─────────►    │    Graph      Forge      │
                         └─────────────────────────┘

  Sentinel (System 1)                  Memory (4 types)
  FS / Clipboard / Calendar / Window   Working / Episodic / Semantic / Procedural
  → Heuristic Filter → Rate Limit     → pgvector + Recursive CTEs
  → Escalate only surprises            → Graph-Augmented Retrieval
```

**Stack:** .NET 10, PostgreSQL 17 + pgvector, Microsoft.Agents.AI + Workflows, Windows UI Automation, Roslyn.

**Inspired by:** Global Workspace Theory (Dehaene), Dual-Process Theory (Kahneman), Generative Agents (Park et al.), Voyager (Wang et al.), Free Energy Principle (Friston).

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

The **Worker** (Sentinel + Signal/Telegram bridges) is optional during development — most of its functionality is still in progress. If you want to run it:

```bash
dotnet run --project backend/src/Leontes.Worker --configuration Release  # Windows only
```

### Signal setup (optional)

Signal lets you message Leontes from your phone via E2E encrypted messaging. It uses [signal-cli-rest-api](https://github.com/bbernhard/signal-cli-rest-api) running in Docker — no Java needed on your machine.

**Signal is entirely optional.** The Worker runs Sentinel regardless. If you skip this section, the bridge logs "Signal bridge is disabled" and does nothing else.

#### Disabling Signal

If you previously set up Signal and want to turn it off:

```bash
# 1. Remove the phone number from Worker secrets (this disables the bridge)
dotnet user-secrets remove "Signal:PhoneNumber" --project backend/src/Leontes.Worker

# 2. Stop the signal-cli container (saves resources)
docker compose stop signal
```

That's it. The Worker will still start normally — Sentinel keeps running, only the Signal bridge is skipped. To re-enable later, set `Signal:PhoneNumber` again and start the container.

If you never configured Signal at all, there's nothing to disable — it's off by default.

#### 1. Start the Signal container

```bash
docker compose up -d signal
```

This starts `signal-cli-rest-api` on port 8081.

#### 2. Register a phone number

You need a dedicated phone number (prepaid SIM or VoIP) that can receive SMS. This number becomes Leontes' Signal identity — it's separate from your personal number.

```bash
# Request SMS verification
curl -X POST http://localhost:8081/v1/register/+YOUR_PHONE_NUMBER

# If a CAPTCHA is required (likely):
# 1. Open https://signalcaptchas.org/registration/generate in your browser
# 2. Solve the CAPTCHA
# 3. Your browser will try to open a signalcaptcha:// link — copy it from the address bar
# 4. Strip the "signalcaptcha://" prefix and pass the rest as the captcha value
curl -X POST http://localhost:8081/v1/register/+YOUR_PHONE_NUMBER \
  -H "Content-Type: application/json" \
  -d '{"captcha": "signal-hcaptcha.YOUR_TOKEN_HERE"}'
```

> **CAPTCHA tokens expire in about 2 minutes.** Solve the CAPTCHA and run the curl command immediately.

#### 3. Verify registration

After receiving the SMS code:

```bash
curl -X POST http://localhost:8081/v1/register/+YOUR_PHONE_NUMBER/verify/CODE
```

#### 4. Configure Leontes Worker

```bash
# Set the registered phone number (this enables the Signal bridge)
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

#### Signal configuration reference

All Signal settings are stored in Worker user secrets. Nothing goes in `appsettings.json`.

| Setting | Required | Default | Description |
|---------|----------|---------|-------------|
| `Signal:PhoneNumber` | Yes (to enable) | — | The registered phone number. If empty or missing, the bridge is disabled. |
| `Signal:AllowedSenders:0` | No | (allow all) | Phone numbers allowed to message Leontes. If empty, all senders are accepted. Add more with `:1`, `:2`, etc. |
| `Signal:BaseUrl` | No | `http://localhost:8081` | URL of the signal-cli-rest-api container. Only change if you moved the port. |
| `Signal:PollIntervalSeconds` | No | `2` | How often the bridge checks for new messages. |
| `Authentication:ApiKey` | Yes (to enable) | — | API key for forwarding messages to the backend. Set by `leontes init`. |

#### Troubleshooting

| Problem | Fix |
|---------|-----|
| Worker logs "Signal bridge is disabled" | Set `Signal:PhoneNumber` in Worker user secrets — see step 4 |
| Worker logs "Signal REST API not available" | Check `docker compose ps signal` — container must be running on port 8081 |
| Registration fails with 403 | CAPTCHA required — see step 2 above |
| Messages ignored silently | Sender not in `AllowedSenders` — check Worker logs for "unknown sender" warning |
| Worker logs "ApiKey not configured" | Run `leontes init` to generate and set the API key |
| Port 8081 already in use | Change the port mapping in `docker-compose.yml` and update `Signal:BaseUrl` in Worker user secrets |

### Telegram setup (optional)

Telegram lets you message Leontes from your phone via the official [Telegram Bot API](https://core.telegram.org/bots/api). No SIM card, no Docker container — just an HTTPS bot token.

**Telegram is entirely optional.** If you skip this section, the bridge logs "Telegram bridge is disabled" and does nothing else.

#### Disabling Telegram

If you previously set up Telegram and want to turn it off:

```bash
# Remove the bot token from Worker secrets (this disables the bridge)
dotnet user-secrets remove "Telegram:BotToken" --project backend/src/Leontes.Worker
```

The Worker will still start normally — Sentinel and Signal keep running, only the Telegram bridge is skipped.

#### 1. Create a Telegram bot

1. Open Telegram and search for **@BotFather**
2. Send `/newbot` and follow the prompts (choose a name and username)
3. BotFather will reply with a **bot token** — copy it

#### 2. Configure Leontes Worker

```bash
# Set the bot token (this enables the Telegram bridge)
dotnet user-secrets set "Telegram:BotToken" "YOUR_BOT_TOKEN" \
  --project backend/src/Leontes.Worker
```

#### 3. Find your Telegram chat ID

Send any message to your bot in Telegram, then run:

```bash
curl https://api.telegram.org/botYOUR_BOT_TOKEN/getUpdates
```

Look for `"chat": { "id": 12345678 }` in the response. That number is your chat ID.

```bash
# Allow your Telegram account to message Leontes
dotnet user-secrets set "Telegram:AllowedChatIds:0" "YOUR_CHAT_ID" \
  --project backend/src/Leontes.Worker
```

#### 4. Start the Worker

```bash
dotnet run --project backend/src/Leontes.Worker --configuration Release
```

The Worker will connect to the Telegram Bot API and start long-polling for messages. Send a message to your bot — Leontes will respond.

#### Telegram configuration reference

All Telegram secrets are stored in Worker user secrets. Non-secret defaults, such as `Telegram:PollTimeoutSeconds`, may live in `appsettings.json`.

| Setting | Required | Default | Description |
|---------|----------|---------|-------------|
| `Telegram:BotToken` | Yes (to enable) | — | The bot token from @BotFather. If empty or missing, the bridge is disabled. |
| `Telegram:AllowedChatIds:0` | No | (allow none) | Telegram chat IDs allowed to message Leontes. If empty, all messages are rejected. Add more with `:1`, `:2`, etc. |
| `Telegram:PollTimeoutSeconds` | No | `30` | Long-poll timeout in seconds. 30 is Telegram's recommended maximum. |
| `Authentication:ApiKey` | Yes (to enable) | — | API key for forwarding messages to the backend. Set by `leontes init`. |

#### Telegram troubleshooting

| Problem | Fix |
|---------|-----|
| Worker logs "Telegram bridge is disabled" | Set `Telegram:BotToken` in Worker user secrets — see step 2 |
| Worker logs "Telegram bot token is invalid" | Verify the token with `curl https://api.telegram.org/botYOUR_TOKEN/getMe` |
| Messages ignored silently | Chat ID not in `AllowedChatIds` — check Worker logs for "unknown chat" warning |
| Worker logs "ApiKey not configured" | Run `leontes init` to generate and set the API key |

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

## Feature Roadmap

| # | Feature | Status |
|---|---|---|
| 10 | CLI Chat | ✅ Implemented |
| 20 | Conversation Memory | ✅ Implemented (superseded by 70) |
| 30 | PoC / Setup Wizard | ✅ Implemented |
| 40 | API Authentication | ✅ Implemented |
| 50 | Signal Support | ✅ Implemented |
| 55 | Proactive Communication | 📋 Specified |
| 60 | Telegram Support | ✅ Implemented |
| 65 | Thinking Pipeline | 📋 Specified |
| 70 | Hierarchical Memory | 📋 Specified |
| 75 | Error Recovery & Resilience | 📋 Specified |
| 80 | Sentinel Intelligence | 📋 Specified |
| 85 | Observability & Cognitive Telemetry | 📋 Specified |
| 90 | Structural Vision | 📋 Specified |
| 95 | Privacy & Data Governance | 📋 Specified |
| 100 | Tool Forge | 📋 Specified |
| 105 | Cost Control & Budget Management | 📋 Specified |
| 110 | Industry Protocol Standards (AG-UI, MCP, A2A) | 📋 Specified |

## Status

Early development. Core infrastructure (CLI, auth, Signal, Telegram) is implemented. The cognitive architecture (17 feature specs) is fully designed and ready for implementation.

## License

AGPL-3.0 — free for personal use. Commercial use requires a [commercial license](mailto:leontes.dev@pm.me).
