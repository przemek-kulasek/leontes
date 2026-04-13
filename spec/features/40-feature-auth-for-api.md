# 40 Auth for API

## Problem

Leontes (AI assistant) runs locally but the API is completely unauthenticated. Any process on the network can call any endpoint. We need authentication to secure API access, even in a single-user local deployment.

## Prerequisites

- Working POC of API, Worker, and CLI
- Signal and MCP are not yet implemented but will be added soon — auth design must accommodate them

## Rules

- Secure, industry-standard approach
- Consistent with existing project patterns (User Secrets, extension methods, etc.)
- No new third-party NuGet packages — use ASP.NET Core shared framework authentication
- Single API key for all clients (CLI, Worker, future Signal/MCP)

## Solution

### API Key Authentication

A static API key shared across all clients. Bearer token scheme via ASP.NET Core authentication middleware.

### Key Format

- 32 cryptographically random bytes, Base64URL-encoded, prefixed with `lnt_`
- Generated via `System.Security.Cryptography.RandomNumberGenerator`

### Key Generation

- Generated once during `leontes init` (setup wizard)
- Lives only in the CLI project — no API endpoint needed (avoids chicken-and-egg problem where API requires auth to generate auth)
- Distributed to all three projects via `dotnet user-secrets set --id <UserSecretsId>`

### Key Storage

All projects use .NET User Secrets under `Authentication:ApiKey`, consistent with how `ConnectionStrings:DefaultConnection` is already handled:

```bash
dotnet user-secrets set "Authentication:ApiKey" "lnt_..." --project backend/src/Leontes.Api
dotnet user-secrets set "Authentication:ApiKey" "lnt_..." --project backend/src/Leontes.Worker
dotnet user-secrets set "Authentication:ApiKey" "lnt_..." --project backend/src/Leontes.Cli
```

### Authentication Flow

```
Client (CLI/Worker/MCP) → Authorization: Bearer lnt_xxx → API middleware → constant-time compare → allow/reject
```

- `/_health` remains public (no auth required) — standard for health checks, monitoring tools expect this
- All `/api/v1/*` endpoints require authorization
- Constant-time comparison (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks
- Returns 401 Unauthorized for missing or invalid keys

### Channel Compatibility

| Channel | Flow | Auth |
|---------|------|------|
| CLI | CLI → HTTP → API | Bearer token from CLI User Secrets |
| Signal | Phone → Signal → Worker → HTTP → API | Bearer token from Worker User Secrets |
| MCP | MCP client → HTTP/SSE → API | Bearer token (same key) |

All channels funnel through the API via HTTP. The auth layer validates the Bearer token regardless of the originating channel.

### Key Rotation

Deferred. For a single-user local app, regenerate via `leontes init` if compromised.

### Out of Scope

- JWT tokens (unnecessary complexity for single-user local deployment)
- Per-channel keys (could be added later for auditing)
- API endpoint for key generation
- Automatic key rotation
