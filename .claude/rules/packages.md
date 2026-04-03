---
paths: "**/*.cs,**/*.ts,**/*.tsx,**/*.csproj,**/package.json,**/Directory.Packages.props,**/Directory.Build.props"
---

## Approved Technology Manifest

**Constraint:** Any technology, library, or tool NOT listed below requires explicit human approval before use — including dev dependencies.

### Core Languages & Runtimes

| Layer | Language / Runtime |
|---|---|
| Backend | C# 14 on .NET 10 |
| Frontend | TypeScript 5 (strict mode) |
| Database | PostgreSQL 17 |

### Primary Frameworks

| Area | Framework |
|---|---|
| Backend API | ASP.NET Core Minimal APIs (.NET 10) |
| Frontend | Next.js (App Router) + React 19 |
| Styling | Tailwind CSS v4 with `@tailwindcss/postcss` |
| i18n | next-intl |

### Approved NuGet Packages

**AI & LLM:** Microsoft.Agents.AI, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI, OllamaSharp

**Data & Persistence:** Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.AspNetCore.Identity.EntityFrameworkCore

**Infrastructure & Resilience:** AspNetCore.HealthChecks.NpgSql, AspNetCore.HealthChecks.UI.Client, Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.DependencyInjection.Abstractions, Serilog.AspNetCore, Serilog.Enrichers.ClientInfo, Serilog.Sinks.Console

**API & External Services:** Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore, Resend, Stripe.net, HtmlAgilityPack

**Testing:** xunit.v3, xunit.runner.visualstudio, Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing, Microsoft.NET.Test.Sdk, coverlet.collector

### Approved NPM Packages

**Core & State:** next, react, react-dom, typescript, @tanstack/react-query, next-intl

**UI & Styling:** tailwindcss, @tailwindcss/postcss, framer-motion, lucide-react

**Development & Tooling:** eslint, eslint-config-next, @types/node, @types/react, @types/react-dom
