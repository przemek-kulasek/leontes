# Project Architecture

## System Overview

High-level description of the system and its main components.

## Components

| Component | Tech | Responsibility |
|-----------|------|----------------|
| Backend API | .NET 10 Minimal API | REST endpoints, auth, business logic |
| Frontend | Next.js + TypeScript | UI, routing, i18n |
| Database | PostgreSQL 17 | Persistence |
| AI Layer | Microsoft.Agents.AI | Agent orchestration, tool execution |

## Data Flow

```
User → Frontend (Next.js) → REST API → Application Layer → Infrastructure (EF Core / AI / External)
                                                          → PostgreSQL
```

## Auth Model

Brief description of auth strategy (JWT + refresh tokens, role-based access).

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| SSE over WebSockets | Simpler for unidirectional streaming |
| No FluentValidation | Domain exceptions + service checks are sufficient |
| Vertical slices | Features implemented end-to-end, not layer-by-layer |

## Infrastructure

- **Dev:** Docker Compose with hot-reload Dockerfiles
- **CI:** GitHub Actions (restore → build → test)
- **Prod:** TBD
