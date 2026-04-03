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
Every new backend (C#) logic branch must have a corresponding xUnit v3 test. Frontend component tests are encouraged but not required — the project has no frontend test runner configured yet.

### Type Safety
No `any` types in TypeScript. No `dynamic` in C#.

### Formatting
Match the existing indentation, naming, and code style of the file you are editing. Do not reformat unrelated code.

### Secrets
Never put API keys, secrets, or credentials in `appsettings.json`, `.env`, or any file that could be committed. Use .NET User Secrets for local backend development. Use environment variables for production. Frontend secrets go in `.env.local` (git-ignored). Ensure `.gitignore` excludes `appsettings.*.json`, `.env`, and `.env.local`.

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
/backend/src/<Project>.Api             — .NET 10 Minimal API host (endpoints, middleware, DI wiring)
/backend/src/<Project>.Application     — Service interfaces, DTOs, feature contracts
/backend/src/<Project>.Domain          — Core entities, value objects, domain exceptions. Zero dependencies.
/backend/src/<Project>.Infrastructure  — EF Core DbContext, external API clients, repositories
/backend/tests                         — Parallel folder structure to /src for xUnit v3 tests
/frontend/src/app/[locale]             — Next.js App Router with dynamic locale segment
/frontend/src/components               — Small, composable UI components grouped by feature
/frontend/src/lib                      — API client, providers, React Query hooks
/frontend/src/types                    — Shared TypeScript type definitions
/frontend/src/i18n                     — next-intl routing, request config, navigation helpers
```

### Clean Architecture Layers

Dependency flows inward only — outer layers reference inner, never the reverse:

1. **Domain** (innermost) — Entities, base Entity class (Guid Id, DateTime Created, Guid CreatedBy, DateTime? LastModified, Guid? LastModifiedBy), domain exceptions (DomainException, ValidationException, NotFoundException). No external dependencies. All IDs are Guids. Audit fields are populated automatically via an EF Core SaveChangesInterceptor — never set them manually. The application User entity extends `IdentityUser<Guid>` (not the base Entity class) — it is a special case managed by ASP.NET Identity.
2. **Application** — Service interfaces, DTOs (records for immutability), IApplicationDbContext abstraction. References Domain only.
3. **Infrastructure** — EF Core DbContext, external API clients, HttpClientFactory with resilience policies, ASP.NET Identity. References Application + Domain.
4. **API** (outermost) — Minimal API endpoints in Endpoints/ folder, global exception handler, extensions for CORS/rate limiting/health checks/logging. Wires DI via AddApplication() / AddInfrastructure() extension methods.

### Communication Patterns

**Data Flow:**
Frontend (Next.js) → REST API (Minimal APIs) → Application Layer (Service Interfaces) → Infrastructure (EF Core / External APIs)

**Real-Time / Streaming:** Use Server-Sent Events (SSE) over HTTP for server-to-client unidirectional streams (AI responses, progress updates, live status). Use SignalR only if bidirectional communication is needed.

**SSE conventions:**
- Backend streams via IAsyncEnumerable or manual Response.WriteAsync with Content-Type: text/event-stream
- Each event has a named type and a JSON payload: `event: <type>\ndata: <json>\n\n`
- Frontend consumes via EventSource or a custom fetch-based reader
- Typed discriminated union for all event types in frontend TypeScript
- Every SSE endpoint must emit a terminal event (done or error) so the client knows when to stop reading
- Define event types per feature (e.g., `session`, `tool_start`, `tool_complete`, `content_delta`, `done`, `error`)

**Stream interruption handling:**
- Track message completion state explicitly (IsComplete flag on assistant messages). User messages are always complete; assistant messages only after full stream received and persisted.
- On interruption with no content: delete the empty message and associated metadata
- On interruption with partial content: preserve with IsComplete = false
- Exclude incomplete assistant messages from future AI context
- Frontend must detect stream termination without done/error event and update UI accordingly

### Validation

No FluentValidation. Validation is handled through:
- Domain exceptions — ValidationException (list of error strings), NotFoundException, base DomainException with status code mapping
- Service-level checks — Business rule enforcement in Application/Infrastructure services
- ASP.NET Identity — Delegates auth validation to the Identity framework
- Frontend — Light validation helpers, server-side validation takes precedence

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

Frontend: Custom error classes (ApiError, UnauthorizedError). UnauthorizedError triggers logout + redirect. Custom parsing for ASP.NET Identity validation errors mapped to i18n strings.

### Database

- PostgreSQL 17 via EF Core 10 (DbContext extending IdentityDbContext)
- Fluent API entity configurations in Configurations/ folder
- Enums stored as strings via OnModelCreating convention
- Initialization: DbContext initializer runs migrations automatically on startup + seeds default roles and users
- Local dev: PostgreSQL container via Docker Compose

**Migration Workflow:**
- Create: `dotnet ef migrations add <DescriptiveName> --project backend/src/<Project>.Infrastructure --startup-project backend/src/<Project>.Api`
- Migration names use PascalCase and describe the change
- Migrations applied automatically on startup — no manual `dotnet ef database update`
- Never edit a migration already applied to a shared environment — create a new corrective migration
- Data migrations go in the initializer or a separate migration, not mixed into schema migrations

**EF Core Query Rules:**
- Always use .AsNoTracking() for read-only queries
- No lazy loading — always load related data explicitly with .Include()
- List endpoints that could grow unbounded must be paginated using PagedRequest / PagedResponse
- Avoid N+1: load related data in a single query, not inside a loop

### Authentication & Authorization

- ASP.NET Identity with bearer tokens (JWT) + refresh token flow
- Auth endpoints: Use MapIdentityApi() for standard endpoints (register, login, refresh). Custom auth endpoints (e.g., GET /api/v1/auth/me) follow the versioned prefix.
- Role-based: Specific roles guard admin endpoints
- User-based: RequireAuthorization() on protected endpoints. IUser interface in Application, CurrentUser implementation in API (uses IHttpContextAccessor). Registered as scoped.

### Email

- ASP.NET Identity email sender interface with two implementations:
  - ResendEmailSender — production, uses the Resend API
  - ConsoleEmailSender — local development, logs email content to console
- Switch via configuration (Email:Provider setting)
- Email used for account confirmation (required before login) and password reset
- Content must be localized per user's locale
- Templates use simple HTML — no external templating engine

### Frontend Architecture

- Next.js with App Router, TypeScript strict mode
- Styling: TailwindCSS + Framer Motion animations + Lucide React icons
- State: TanStack React Query (server state) + React Context (auth) + local useState
- i18n: next-intl with [locale] dynamic segment
- Components: Small (<100 lines), feature-grouped. Named exports for components, default exports for pages/layouts.

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

**Token / Usage Tracking:**
- Wrap the AI chat client in a tracking decorator that counts input/output tokens per request
- Store usage per user; enforce plan limits at the endpoint level before invoking the agent
- Expose a usage endpoint for the frontend

**LLM Providers:**
- Configurable via AiProvider:Provider setting — do not hardcode a provider
- Local dev uses Ollama; production uses a cloud provider
- All provider-specific wiring belongs in Infrastructure; Application and Domain are provider-agnostic

### Local Development

Each service has two Dockerfiles:
- `Dockerfile.dev` — local development, uses `dotnet watch` (backend) / Next.js dev server (frontend) for hot reload
- `Dockerfile` — production, multi-stage build with minimal runtime image

**Docker Compose** runs the full stack locally using the dev Dockerfiles:
```
postgres:    # PostgreSQL 17, port 5432, health check via pg_isready
backend:     # .NET with watch mode (hot reload), port 5186 — depends_on: postgres
frontend:    # Next.js dev server, port 3000 — depends_on: backend
```

**Commands:**
```bash
# Backend
dotnet build backend/
dotnet test backend/
dotnet run --project backend/src/<Project>.Api

# Frontend
cd frontend && npm install
cd frontend && npm run dev
cd frontend && npm run build

# Full stack
docker compose up
```

Health checks: backend exposes `/_health` endpoint.

Frontend dev proxy: Use next.config.ts rewrites to route API calls to the backend container.

Resilience: AddStandardResilienceHandler() on all external HTTP clients.

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

### CORS
- Allow only the frontend origin (e.g., `http://localhost:3000` in dev, production Vercel domain in prod)
- No wildcard (`*`) origins — ever
- Allow credentials (cookies, authorization headers)
- Allowed methods: GET, POST, PUT, PATCH, DELETE, OPTIONS
- Allowed headers: Content-Type, Authorization, X-Correlation-Id
- Origins configured via `appsettings.json` / environment variables, not hardcoded

### Rate Limiting
- Use ASP.NET Core's built-in rate limiter middleware with named policies in `AddRateLimiting()`
- Global default: Fixed window — 100 requests per minute per IP
- Auth endpoints: 10 requests per minute per IP
- Return `429 Too Many Requests` with `Retry-After` header
- These are starting points — adjust as needed

### Content Security Policy (CSP)
```
Content-Security-Policy:
  default-src 'self';
  script-src 'self';
  style-src 'self' 'unsafe-inline';
  img-src 'self' data: https:;
  font-src 'self';
  connect-src 'self' <api-origin>;
  frame-ancestors 'none';
  base-uri 'self';
  form-action 'self';
```

### Security Headers

Configure security headers in both layers:
- **Backend:** Set headers via middleware (e.g., `UseSecurityHeaders()` extension method on WebApplication)
- **Frontend:** Set headers in `next.config.ts` via the `headers()` async config function

| Header | Value | Purpose |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | Prevent MIME-type sniffing |
| `X-Frame-Options` | `DENY` | Prevent clickjacking |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Limit referrer leakage |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | Disable unnecessary browser APIs |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | Enforce HTTPS (production only) |

### Input Validation & Sanitization
- Never trust client input — validate on the server
- Use parameterized queries exclusively (EF Core default)
- Sanitize user-provided HTML content with an allowlist approach
- Validate file uploads: check MIME type, enforce size limits, never store in web-accessible directory with original filename

### Authentication Security
- Passwords: Delegate to ASP.NET Identity (bcrypt/PBKDF2)
- Tokens: JWT access tokens 15-30 min expiry. Refresh tokens 7 days, single-use, rotated on refresh
- Failed login lockout: 5 failed attempts → 15 minutes lockout



Language-specific conventions (C#, TypeScript, logging, REST API, testing, copywriting, approved packages) are in `.github/instructions/` and load automatically when editing relevant file types.
