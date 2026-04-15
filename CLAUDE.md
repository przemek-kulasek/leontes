# Leontes

## Persona

Senior software engineer. Full persona definition in `.claude/rules/developer.md` — loaded automatically when editing code files.

---

## Agent Rules

### Packages
Never use a NuGet or NPM package not on the approved list in `.claude/rules/packages.md`. If a task genuinely requires a new package, stop and ask before proceeding.

### When to Ask
Ask only when the answer is not covered by this spec — new feature scope, architectural decisions, or ambiguous business logic. Do not ask about naming, formatting, or anything already defined here.

### Test Coverage
Every new backend (C#) logic branch must have a corresponding xUnit v3 test.

### Type Safety
No `dynamic` in C#.

### Formatting
Match the existing indentation, naming, and code style of the file you are editing. Do not reformat unrelated code.

### Secrets
Never put secrets in `appsettings.json` or any committed file. Use .NET User Secrets for local development. Environment variables for production.

### Git
Never push directly to `main` or `develop`. Always use a feature branch (`feature/<name>`).

### Build Health
Zero errors, warnings, or suppressions unless explicitly intentional and commented.

### Build Pipeline
GitHub Actions CI via `.github/workflows/ci.yml`. Triggers on push to `main`, `develop`, `feature/*` and PRs targeting `main` or `develop`. Steps: restore → build → test (`dotnet test backend/`). Must pass before merging.

### Documentation
Keep `README.md` up to date when adding or removing features, dependencies, or setup steps.

---

## Architecture

### Directory Layout

```
/backend/src/Leontes.Api             — .NET 10 Minimal API host + Processing Loop (IHostedService)
/backend/src/Leontes.Worker          — .NET 10 Windows Service (Sentinel + Signal/Telegram bridges)
/backend/src/Leontes.Cli             — .NET 10 console app / dotnet tool (installed as `leontes`)
/backend/src/Leontes.Application     — Service interfaces, DTOs, feature contracts
/backend/src/Leontes.Domain          — Core entities, value objects, domain exceptions (zero dependencies)
/backend/src/Leontes.Infrastructure  — EF Core DbContext, external API clients, repositories
/backend/tests                       — xUnit v3 tests mirroring /src structure
/spec                                — Project and feature specifications
```

### Host Architecture

Three executable projects, two always-running hosts + one CLI tool:

1. **Leontes.Api** — HTTP endpoints + Processing Loop (`IHostedService`). The brain. Handles chat requests from CLI (via HTTP) and from Signal/Telegram (forwarded by Worker). Runs the LLM, tools, Synapse Graph queries. Includes rate limiting, CORS, auto-migration on startup.
2. **Leontes.Worker** — Windows Service (`UseWindowsService()`) running Sentinel + messaging bridges (Signal, Telegram). Always on. Forwards Signal/Telegram messages to the API. Sends notifications when Sentinel triggers.
3. **Leontes.Cli** — dotnet tool installed globally as `leontes`. Commands: `leontes init` (setup wizard), `leontes chat` (interactive chat), `leontes` (default: chat). Communicates with the API via HTTP.

### Clean Architecture Layers

Dependency flows inward only. Api, Worker, and Cli are outer-layer hosts — they all share the same inner layers:

1. **Domain** (innermost) — Base Entity class (Guid Id, DateTime Created, Guid CreatedBy, DateTime? LastModified, Guid? LastModifiedBy). All IDs are Guids. Audit fields auto-populated via SaveChangesInterceptor — never set them manually. Domain exceptions: DomainException, ValidationException, NotFoundException.
2. **Application** — Service interfaces, DTOs (records), IApplicationDbContext, PagedRequest/PagedResponse. References Domain only.
3. **Infrastructure** — EF Core DbContext, ApplicationDbContextInitializer (auto-migration + seeding), external API clients, HttpClientFactory with resilience. References Application + Domain.
4. **Api** (outermost) — Minimal API endpoints in Endpoints/ folder, global exception handler, extensions for health checks/logging/CORS/rate limiting. DI wiring via AddApplication() / AddInfrastructure(). Auto-migrates database on startup.
5. **Worker** (outermost) — Windows Service hosting Sentinel background services and messaging bridges (Signal, Telegram). DI wiring via AddApplication() / AddInfrastructure().
6. **Cli** (outermost) — Standalone console app, no project references to backend layers. Communicates with Api via HTTP only.

### Communication

- Three channels: CLI (terminal), Signal (E2E encrypted mobile), and Telegram (official Bot API), all feeding into one async processing loop hosted in the Api via a shared `IMessagingClient` abstraction
- CLI communicates with Api via HTTP; Signal and Telegram messages are received by Worker and forwarded to Api
- Data flow: CLI/Signal/Telegram → Queue → Processing Loop → Synapse Graph → LLM + Tools → Response → Original Channel
- Sentinel triggers: OS Events → Pattern Match → AI Layer (if needed) → CLI/Signal/Telegram notification
- SSE for streaming responses from backend to CLI client
- SSE: backend streams via IAsyncEnumerable or Response.WriteAsync with `text/event-stream`. Named event types with JSON payload (`event: <type>\ndata: <json>\n\n`). Terminal event (done/error) required.
- Stream interruption: track IsComplete flag on assistant messages. On interruption with no content: delete the empty message and metadata. On interruption with partial content: preserve with `IsComplete = false`. Exclude incomplete assistant messages from future AI context.

### Validation

No FluentValidation. Use domain exceptions and service-level checks.

### Error Handling

Global ExceptionHandler (ExceptionHandler.cs implementing IExceptionHandler) returns RFC 9457 ProblemDetails. Mapping: ValidationException → 400, NotFoundException → 404, unhandled → 500.

### Database

- PostgreSQL 17 via EF Core 10, Fluent API configs in Configurations/ folder, enums as strings
- Initialization: runs migrations automatically on startup + seeds default data
- Migrations: `dotnet ef migrations add <DescriptiveName> --project backend/src/Leontes.Infrastructure --startup-project backend/src/Leontes.Api`
- Migration names use PascalCase and describe the change. Never edit applied migrations. Data migrations go in the initializer or a separate migration, not mixed into schema migrations.
- Query rules: .AsNoTracking() for reads, no lazy loading, explicit .Include(), paginate unbounded lists (PagedRequest/PagedResponse), avoid N+1

### Synapse Graph (Knowledge Graph)

- PostgreSQL 17 + pgvector for entities, relationships, and semantic search
- Entity types: People, Files, Projects — linked by relationships
- Contextual resolution: "send this to the lead dev" → person lookup from Git/email history
- Vector embeddings stored alongside entities for semantic search
- Tool usage tracked in graph; unused tools pruned automatically

### Auth

- Single-user, local deployment
- API key or JWT for CLI communication with backend
- Signal: bot registration during `leontes init`
- Telegram: bot token from @BotFather, configured during `leontes init`
- JWT secret auto-generated by setup wizard, stored in .NET User Secrets

### AI / Agents

- Microsoft.Agents.AI + Microsoft.Agents.AI.Workflows for cognitive pipeline
- Tools as classes in Infrastructure/. Tool metadata (name, description) must be clear and human-readable.
- Single agent by default, split only when domains are clearly separate
- Two model tiers: Large (Plan, Execute) and Small (Reflect, Consolidation, Sentinel). Registered as keyed `IChatClient` via `[FromKeyedServices("Large")]` / `[FromKeyedServices("Small")]`
- Provider configurable (AiProvider:Models:Large/Small), Ollama for dev, cloud for prod
- Agent personality defined in `persona.md` — loaded at startup as system instructions. Per-stage temperature configured via `Persona:StageSettings`
- Token tracking: `ChatResponse.Usage` from Microsoft.Extensions.AI provides InputTokenCount/OutputTokenCount. `OpenTelemetryAgent` wrapper for observability. Custom `ITokenMeter` decorator for budget enforcement.

### Sentinel (Proactive Engine)

- Background service hosted in Leontes.Worker as an `IHostedService`
- Monitors OS events: file system watcher, clipboard, calendar, active window
- Defines interfaces for each input source: IFileSystemWatcher, IClipboardMonitor, ICalendarMonitor, IActiveWindowMonitor
- Pattern rules trigger suggestions or autonomous actions
- Delivered via CLI, Signal, or Telegram notification

### Structural Vision

- Windows UI Automation to read application UI as structured element tree
- Accessibility APIs for interaction — no screenshots, no simulated clicks

### Tool Forge (Self-Extending Agent)

- Agent writes tool classes when capability gap detected or repeated pattern observed
- Flow: generate tool class → compile via Roslyn → run test → user approval → register in catalog
- Usage tracked in Synapse Graph; unused tools pruned automatically
- No unreviewed code runs — user must approve before registration

### Setup Wizard

- Implemented as `leontes init` command in Leontes.Cli
- Steps: PostgreSQL via Docker Compose → AI provider + API key → Signal bot registration → Telegram bot setup → Sentinel defaults → auth secret generation
- All secrets stored in .NET User Secrets

### Local Dev

Docker Compose runs PostgreSQL. All .NET projects run natively for faster iteration and debugger access. Ollama must be running locally.

```bash
docker compose up -d db                              # PostgreSQL only
dotnet run --project backend/src/Leontes.Api         # API (auto-migrates DB on startup)
dotnet run --project backend/src/Leontes.Cli         # CLI chat
dotnet run --project backend/src/Leontes.Worker      # Worker / Sentinel (Windows only)
dotnet build backend/ && dotnet test backend/ --configuration Release  # Build and test
```

Health checks: backend exposes `/_health` endpoint.

Resilience: AddStandardResilienceHandler() on all external HTTP clients.

Rate limiting: Fixed-window rate limiter (100 requests/minute per client IP) via ASP.NET Core built-in middleware.

---

## Good Practices

- Meaningful names, small functions (<20 lines), ≤3 arguments, CQS, no side effects, DRY
- No AI-generated summary comments. Comment "why", not "what". Self-documenting code first.
- Boy Scout Rule, fail fast with guard clauses, composition over inheritance, explicit over implicit, DI
- SOLID: SRP, OCP, LSP, ISP, DIP
- YAGNI: no speculative development, simplest solution, no single-implementation abstractions, delete unused code
- Domain exceptions for violated contracts; Result/discriminated unions for expected non-error outcomes

---

## Security

### Input Validation
Server-side always. Parameterized queries (EF Core) — never concatenate user input into SQL.

### Auth Security
JWT for CLI ↔ backend communication. Secret auto-generated, stored in .NET User Secrets.

### Secrets Management
- Never commit secrets — `.gitignore` must exclude `appsettings.*.json`, `.env`, `.env.local`

---

Language-specific conventions (C#, logging, REST API, testing, approved packages) are in `.claude/rules/` and load automatically when editing relevant file types.
