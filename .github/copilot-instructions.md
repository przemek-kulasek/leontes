# Project Instructions

## Persona

Senior software engineer. Full persona definition in `.github/instructions/developer.instructions.md` — loaded automatically when editing code files.

---

## Agent Rules

### Packages
Never use a NuGet or NPM package (including dev dependencies) that is not on the approved list in `.github/instructions/packages.instructions.md`. If a task genuinely requires a new package, stop and ask before proceeding.

### When to Ask
Ask before proceeding only when the answer is not covered by this spec — for example: new feature scope, architectural decisions, or ambiguous business logic. Do not ask about naming conventions, formatting, or anything this spec already defines — just follow the spec.

### Test Coverage
Every new backend (C#) logic branch must have a corresponding xUnit v3 test.

### Type Safety
No `dynamic` in C#.

### Formatting
Match the existing indentation, naming, and code style of the file you are editing. Do not reformat unrelated code.

### Secrets
Never put API keys, secrets, or credentials in `appsettings.json` or any file that could be committed. Use .NET User Secrets for local development. Ensure `.gitignore` excludes `appsettings.*.json`.

### Git
Never push directly to `main` or `develop`. Always push to a feature branch (`feature/<name>`).

### Build Pipeline
GitHub Actions CI via `.github/workflows/ci.yml`. Triggers on:
- Push to `main`, `develop`, and `feature/*` branches
- Pull requests targeting `main` or `develop`

Steps: restore → build → test (`dotnet test backend/`). Must pass before merging.

### Documentation
Keep `README.md` up to date when adding or removing features, dependencies, or setup steps.

### Build Health
Zero errors, warnings, or suppressions in the build and runtime output — unless explicitly intentional and commented as such.

---

## Project Structure & Architecture

### Directory Layout

Follow this standard structure for all new files:

```
/backend/src/Leontes.Api             — .NET 10 Minimal API host + Processing Loop (IHostedService)
/backend/src/Leontes.Worker          — .NET 10 Windows Service (Sentinel + Signal bridge)
/backend/src/Leontes.Cli             — .NET 10 console app / dotnet tool (installed as `leontes`)
/backend/src/Leontes.Application     — Service interfaces, DTOs, feature contracts
/backend/src/Leontes.Domain          — Core entities, value objects, domain exceptions. Zero dependencies.
/backend/src/Leontes.Infrastructure  — EF Core DbContext, external API clients, repositories
/backend/tests                       — Parallel folder structure to /src for xUnit v3 tests
/spec                                — Project and feature specifications
```

### Host Architecture

Three executable projects, two always-running hosts + one CLI tool:

1. **Leontes.Api** — HTTP endpoints + Processing Loop (`IHostedService`). The brain. Handles chat requests from CLI (via HTTP) and from Signal (forwarded by Worker). Runs the LLM, tools, Synapse Graph queries. Includes rate limiting, CORS, auto-migration on startup.
2. **Leontes.Worker** — Windows Service (`UseWindowsService()`) running Sentinel + Signal bridge. Always on. Forwards Signal messages to the API. Sends notifications when Sentinel triggers.
3. **Leontes.Cli** — dotnet tool installed globally as `leontes`. Commands: `leontes init` (setup wizard), `leontes chat` (interactive chat), `leontes` (default: chat). Communicates with the API via HTTP.

### Clean Architecture Layers

Dependency flows inward only — outer layers reference inner, never the reverse. Api, Worker, and Cli are outer-layer hosts sharing the same inner layers:

1. **Domain** (innermost) — Entities, base Entity class (Guid Id, DateTime Created, Guid CreatedBy, DateTime? LastModified, Guid? LastModifiedBy), domain exceptions (DomainException, ValidationException, NotFoundException). No external dependencies. All IDs are Guids. Audit fields are populated automatically via an EF Core SaveChangesInterceptor — never set them manually.
2. **Application** — Service interfaces, DTOs (records for immutability), IApplicationDbContext abstraction, PagedRequest/PagedResponse. References Domain only.
3. **Infrastructure** — EF Core DbContext, ApplicationDbContextInitializer (auto-migration + seeding), external API clients, HttpClientFactory with resilience policies. References Application + Domain.
4. **Api** (outermost) — Minimal API endpoints in Endpoints/ folder, global exception handler, extensions for health checks/logging/CORS/rate limiting. Wires DI via AddApplication() / AddInfrastructure() extension methods. Auto-migrates database on startup.
5. **Worker** (outermost) — Windows Service hosting Sentinel background services and Signal bridge. Wires DI via AddApplication() / AddInfrastructure().
6. **Cli** (outermost) — Standalone console app, no project references to backend layers. Communicates with Api via HTTP only.

### Communication Patterns

**Channels:** CLI (terminal on host) and Signal (E2E encrypted mobile), both feeding into one async processing loop hosted in the Api. CLI communicates via HTTP; Signal messages are received by Worker and forwarded to Api.

**Data Flow:**
CLI/Signal → Queue → Processing Loop → Synapse Graph → LLM + Tools → Response → Original Channel

**Sentinel Triggers:**
OS Events → Pattern Match → AI Layer (if needed) → CLI/Signal notification

**Streaming:** SSE over HTTP for server-to-client streaming (AI responses, progress).
- Backend streams via IAsyncEnumerable or manual Response.WriteAsync with Content-Type: text/event-stream
- Each event has a named type and a JSON payload: `event: <type>\ndata: <json>\n\n`
- Every SSE endpoint must emit a terminal event (done or error) so the client knows when to stop reading

**Stream interruption handling:**
- Track message completion state explicitly (IsComplete flag on assistant messages).
- On interruption with no content: delete the empty message and associated metadata
- On interruption with partial content: preserve with IsComplete = false
- Exclude incomplete assistant messages from future AI context

### Validation

No FluentValidation. Validation is handled through:
- Domain exceptions — ValidationException (list of error strings), NotFoundException, base DomainException with status code mapping
- Service-level checks — Business rule enforcement in Application/Infrastructure services

### Error Handling

Global Exception Handler (ExceptionHandler.cs implementing IExceptionHandler) returns consistent RFC 9457 Problem Details:

```json
{
  "title": "Validation Failed",
  "status": 400,
  "detail": "Description of what went wrong",
  "type": "https://http.dev/400",
  "instance": "/api/resource",
  "errors": ["specific validation error"]
}
```

Status Code Mapping: ValidationException → 400, NotFoundException → 404, unhandled → 500.

### Database

- PostgreSQL 17 via EF Core 10
- Fluent API entity configurations in Configurations/ folder
- Enums stored as strings via OnModelCreating convention
- Initialization: DbContext initializer runs migrations automatically on startup + seeds default data
- Local dev: PostgreSQL container via Docker Compose

**Migration Workflow:**
- Create: `dotnet ef migrations add <DescriptiveName> --project backend/src/Leontes.Infrastructure --startup-project backend/src/Leontes.Api`
- Migration names use PascalCase and describe the change
- Migrations applied automatically on startup — no manual `dotnet ef database update`
- Never edit a migration already applied to a shared environment — create a new corrective migration
- Data migrations go in the initializer or a separate migration, not mixed into schema migrations

**EF Core Query Rules:**
- Always use .AsNoTracking() for read-only queries
- No lazy loading — always load related data explicitly with .Include()
- List endpoints that could grow unbounded must be paginated using PagedRequest / PagedResponse
- Avoid N+1: load related data in a single query, not inside a loop

### Synapse Graph (Knowledge Graph)

- PostgreSQL 17 + pgvector for entities, relationships, and semantic search
- Entity types: People, Files, Projects — linked by relationships
- Contextual resolution: "send this to the lead dev" → person lookup from Git/email history
- Vector embeddings stored alongside entities for semantic search
- Tool usage tracked in graph; unused tools pruned automatically

### Authentication

- Single-user, local deployment
- API key or JWT for CLI communication with backend
- Signal: bot registration during `leontes init`
- JWT secret auto-generated by setup wizard, stored in .NET User Secrets

### AI / Agent Conventions

The project uses Microsoft.Agents.AI to build LLM-powered agents backed by real tools.

**Tool Structure:**
- Each tool is a class in Infrastructure/ implementing the expected tool interface
- Register tools via DI in AddInfrastructure()
- Tools must be deterministic and side-effect-free where possible
- Tool metadata (name, description) must be clear and human-readable

**Agent Architecture:**
- Start with a single agent; split into multiple specialized agents only when domains are clearly separate
- Never build multi-agent orchestration speculatively

**LLM Providers:**
- Configurable via AiProvider:Provider setting — do not hardcode a provider
- Local dev uses Ollama; production uses a cloud provider
- All provider-specific wiring belongs in Infrastructure; Application and Domain are provider-agnostic

### Sentinel (Proactive Engine)

- Background service hosted in Leontes.Worker as an `IHostedService`
- Monitors OS events: file system watcher, clipboard, calendar, active window
- Defines interfaces for each input source: IFileSystemWatcher, IClipboardMonitor, ICalendarMonitor, IActiveWindowMonitor
- Pattern rules trigger suggestions or autonomous actions
- Delivered via CLI or Signal notification

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
- Steps: PostgreSQL via Docker Compose → AI provider + API key → Signal bot registration → Sentinel defaults → auth secret generation
- All secrets stored in .NET User Secrets

### Local Development

Docker Compose runs PostgreSQL + backend with hot-reload via `dotnet watch`. Worker runs natively on Windows (needs OS APIs for Sentinel).

**Commands:**
```bash
# Backend
dotnet build backend/
dotnet test backend/
dotnet run --project backend/src/Leontes.Api

# Worker (Windows only)
dotnet run --project backend/src/Leontes.Worker

# CLI
dotnet run --project backend/src/Leontes.Cli

# Full stack
docker compose up
```

Health checks: backend exposes `/_health` endpoint.

Resilience: AddStandardResilienceHandler() on all external HTTP clients.

Rate limiting: Fixed-window rate limiter (100 requests/minute per client IP) via ASP.NET Core built-in middleware.

---

## Good Practices

- Meaningful names, small functions (<20 lines), ≤3 arguments, CQS, no side effects, DRY
- No AI-generated summary comments. Explain yourself in code first. Only comment "Why" (business logic/edge cases), never "What".
- Boy Scout Rule, fail fast with guard clauses, composition over inheritance, explicit over implicit (constants/enums, no magic values), DI
- SOLID: SRP, OCP, LSP, ISP, DIP
- YAGNI: no speculative development, simplest solution, no single-implementation abstractions, delete unused code immediately
- Domain exceptions for violated contracts; Result/discriminated unions for expected non-error outcomes. No exceptions for control flow.

---

## Security

### Input Validation
- Never trust client input — validate on the server
- Use parameterized queries exclusively (EF Core default)

### Auth Security
- JWT for CLI ↔ backend communication. Secret auto-generated, stored in .NET User Secrets.

### Secrets Management
- Never commit secrets — `.gitignore` must exclude `appsettings.*.json`

Language-specific conventions (C#, logging, REST API, testing, approved packages) are in `.github/instructions/` and load automatically when editing relevant file types.
