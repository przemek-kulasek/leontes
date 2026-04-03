# Leontes

## Development

### Local Setup

**Docker Compose** runs the full stack locally:
```bash
docker compose up
```

Services: postgres (port 5432) → backend (port 5186) → frontend (port 3000). Health checks gate the startup chain.

**Without Docker:**
```bash
# Backend
dotnet build backend/
dotnet test backend/
dotnet run --project backend/src/<Project>.Api

# Frontend
cd frontend && npm install
cd frontend && npm run dev
```

### Secrets Management

- **Backend (local):** Use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) — never `appsettings.json`
- **Frontend (local):** Use `.env.local` (git-ignored) — never `.env` with real values
- **If a secret is accidentally committed:** Rotate immediately — treat it as compromised

### CI/CD

GitHub Actions CI via `.github/workflows/ci.yml`. Triggers on push to `main`, `develop`, `feature/*` and PRs targeting `main` or `develop`. Steps: restore → build → test. No deployment pipeline configured yet.

**Dependabot** is enabled for automated dependency update PRs.

### OWASP Top 10 Mitigations

| Risk | How we mitigate |
|---|---|
| A01 Broken Access Control | Role-based auth, user-scoped data queries |
| A02 Cryptographic Failures | HTTPS everywhere, no secrets in code, ASP.NET Identity hashing |
| A03 Injection | EF Core parameterized queries, no raw SQL concatenation, no `eval()` |
| A04 Insecure Design | Clean architecture separation, least privilege |
| A05 Security Misconfiguration | No default credentials, no stack traces in production, security headers |
| A06 Vulnerable Components | Dependabot enabled, approved package allowlist |
| A07 Auth Failures | Short-lived JWTs, refresh token rotation, account lockout |
| A08 Data Integrity Failures | Server-side validation, no untrusted deserialization |
| A09 Logging Failures | Structured logging with correlation IDs, never log secrets |
| A10 SSRF | Allowlist for backend-fetched URLs |