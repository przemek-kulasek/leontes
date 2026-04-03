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
Every new backend (C#) logic branch must have a corresponding xUnit v3 test. Frontend component tests are encouraged but not required — the project has no frontend test runner configured yet.

### Type Safety
No `any` in TypeScript. No `dynamic` in C#.

### Formatting
Match the existing indentation, naming, and code style of the file you are editing. Do not reformat unrelated code.

### Secrets
Never put secrets in `appsettings.json`, `.env`, or any committed file. Use .NET User Secrets for backend, `.env.local` for frontend. Environment variables for production.

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
/backend/src/<Project>.Api             — .NET 10 Minimal API host
/backend/src/<Project>.Application     — Service interfaces, DTOs, feature contracts
/backend/src/<Project>.Domain          — Core entities, value objects, domain exceptions (zero dependencies)
/backend/src/<Project>.Infrastructure  — EF Core DbContext, external API clients, repositories
/backend/tests                         — xUnit v3 tests mirroring /src structure
/frontend/src/app/[locale]             — Next.js App Router with dynamic locale segment
/frontend/src/components               — Small, composable UI components grouped by feature
/frontend/src/lib                      — API client, providers, React Query hooks
/frontend/src/types                    — Shared TypeScript type definitions
/frontend/src/i18n                     — next-intl routing, request config, navigation helpers
```

### Clean Architecture Layers

Dependency flows inward only:

1. **Domain** (innermost) — Base Entity class (Guid Id, DateTime Created, Guid CreatedBy, DateTime? LastModified, Guid? LastModifiedBy). All IDs are Guids. Audit fields auto-populated via SaveChangesInterceptor — never set them manually. Domain exceptions: DomainException, ValidationException, NotFoundException. User entity extends `IdentityUser<Guid>`, not base Entity.
2. **Application** — Service interfaces, DTOs (records), IApplicationDbContext. References Domain only.
3. **Infrastructure** — EF Core DbContext, external API clients, HttpClientFactory with resilience, ASP.NET Identity. References Application + Domain.
4. **API** (outermost) — Minimal API endpoints in Endpoints/ folder, global exception handler, extensions for CORS/rate limiting/health checks/logging. DI wiring via AddApplication() / AddInfrastructure().

### Communication

- Data Flow: Frontend (Next.js) → REST API (Minimal APIs) → Application Layer → Infrastructure (EF Core / External APIs)
- SSE for server-to-client streaming (AI responses, progress). SignalR only if bidirectional needed.
- SSE: backend streams via IAsyncEnumerable or Response.WriteAsync with `text/event-stream`. Named event types with JSON payload (`event: <type>\ndata: <json>\n\n`). Terminal event (done/error) required. Define event types per feature (e.g., `session`, `tool_start`, `tool_complete`, `content_delta`, `done`, `error`). Frontend consumes via EventSource or custom fetch-based reader. Typed discriminated union for all event types in frontend TypeScript.
- Stream interruption: track IsComplete flag on assistant messages. User messages are always complete; assistant messages only after full stream received and persisted. On interruption with no content: delete the empty message and metadata. On interruption with partial content: preserve with `IsComplete = false`. Exclude incomplete assistant messages from future AI context. Frontend must detect stream termination without done/error event and update UI accordingly.

### Validation

No FluentValidation. Use domain exceptions, service-level checks, ASP.NET Identity, and frontend light helpers.

### Error Handling

Global ExceptionHandler (ExceptionHandler.cs implementing IExceptionHandler) returns RFC 9457 ProblemDetails. Mapping: ValidationException → 400, NotFoundException → 404, unhandled → 500. Frontend: ApiError and UnauthorizedError classes. Custom parsing for ASP.NET Identity validation errors mapped to i18n strings.

### Database

- PostgreSQL 17 via EF Core 10 (DbContext extending IdentityDbContext), Fluent API configs in Configurations/ folder, enums as strings
- Initialization: runs migrations automatically on startup + seeds default roles and users
- Migrations: `dotnet ef migrations add <DescriptiveName> --project backend/src/<Project>.Infrastructure --startup-project backend/src/<Project>.Api`
- Migration names use PascalCase and describe the change. Never edit applied migrations. Data migrations go in the initializer or a separate migration, not mixed into schema migrations.
- Query rules: .AsNoTracking() for reads, no lazy loading, explicit .Include(), paginate unbounded lists (PagedRequest/PagedResponse), avoid N+1

### Auth

- ASP.NET Identity + JWT + refresh token flow (expiry details in Security section)
- MapIdentityApi() for standard endpoints (framework-provided, not versioned). Custom auth endpoints versioned under /api/v1/.
- Role-based: specific roles guard admin endpoints. RequireAuthorization() on protected endpoints.
- IUser in Application, CurrentUser in API (uses IHttpContextAccessor, scoped)

### Email

- ResendEmailSender (prod) / ConsoleEmailSender (dev, no API key needed), switched via config (Email:Provider)
- Email used for account confirmation (required before login) and password reset
- Localized templates, simple HTML

### Frontend

- Next.js with App Router, TypeScript strict mode
- Styling: TailwindCSS + Framer Motion animations + Lucide React icons
- State: TanStack React Query (server state) + React Context (auth) + local useState
- i18n: next-intl with [locale] dynamic segment
- Components: Small (<100 lines), feature-grouped. Named exports for components, default exports for pages/layouts.

### AI / Agents

- Microsoft.Agents.AI, tools as classes in Infrastructure/
- Tools must be deterministic and side-effect-free where possible. Tool metadata (name, description) must be clear and human-readable.
- Single agent by default, split only when domains are clearly separate
- Token tracking decorator, usage limits enforced at endpoint level. Expose a usage endpoint for the frontend.
- Provider configurable (AiProvider:Provider), Ollama for dev, cloud for prod

### Local Dev

Each service has two Dockerfiles: `Dockerfile.dev` (hot reload via `dotnet watch`/Next.js dev server) and `Dockerfile` (production multi-stage build with minimal runtime image).

```bash
docker compose up                                    # Full stack (uses dev Dockerfiles)
dotnet build backend/ && dotnet test backend/        # Backend
dotnet run --project backend/src/<Project>.Api       # Run backend directly
cd frontend && npm install && npm run dev            # Frontend
```

Health checks: backend exposes `/_health` endpoint.

Frontend dev proxy: Use next.config.ts rewrites to route API calls to the backend container.

Resilience: AddStandardResilienceHandler() on all external HTTP clients.

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

### CORS
Frontend origin only, no wildcards, credentials allowed. Methods: GET, POST, PUT, PATCH, DELETE, OPTIONS. Headers: Content-Type, Authorization, X-Correlation-Id. Origins from config, not hardcoded.

### Rate Limiting
ASP.NET Core built-in with named policies in `AddRateLimiting()`. Apply via `.RequireRateLimiting("policyName")`. Global: 100/min/IP. Auth endpoints: 10/min/IP. Return 429 with Retry-After. These are starting points — adjust as needed.

### CSP
```
default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';
img-src 'self' data: https:; font-src 'self'; connect-src 'self' <api-origin>;
frame-ancestors 'none'; base-uri 'self'; form-action 'self';
```

### Security Headers
Configure in both layers: backend via `UseSecurityHeaders()` middleware, frontend via `next.config.ts` `headers()` function.

X-Content-Type-Options: nosniff | X-Frame-Options: DENY | Referrer-Policy: strict-origin-when-cross-origin | Permissions-Policy: camera=(), microphone=(), geolocation=() | HSTS: max-age=31536000; includeSubDomains (prod only)

### Input Validation
Server-side always. Parameterized queries (EF Core) — never concatenate user input into SQL. Sanitize HTML with allowlist. Validate file uploads: check MIME type, enforce size limits, never store in web-accessible directory with original filename.

### Auth Security
ASP.NET Identity for passwords. JWT 15-30 min, refresh 7 days (single-use, rotated). Lockout after 5 failed attempts for 15 min.

### Secrets Management
- Never commit secrets — `.gitignore` must exclude `appsettings.*.json`, `.env`, `.env.local`

---

Language-specific conventions (C#, TypeScript, logging, REST API, testing, copywriting, approved packages) are in `.claude/rules/` and load automatically when editing relevant file types.
