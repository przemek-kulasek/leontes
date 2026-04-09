---
applyTo: "**/*.cs,**/*.ts,**/*.tsx,**/*.csproj,**/package.json,**/Directory.Packages.props,**/Directory.Build.props"
---

## Approved Technology Manifest

**Constraint:** Any technology, library, or tool NOT listed below requires explicit human approval before use — including dev dependencies.

### Core Languages & Runtimes

| Layer | Language / Runtime |
|---|---|
| Backend | C# 14 on .NET 10 |
| Database | PostgreSQL 17 |

### Primary Frameworks

| Area | Framework |
|---|---|
| Backend API | ASP.NET Core Minimal APIs (.NET 10) |

### Approved NuGet Packages

**AI & LLM:** Microsoft.Agents.AI, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI, OllamaSharp

**Data & Persistence:** Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design, Npgsql.EntityFrameworkCore.PostgreSQL, Pgvector.EntityFrameworkCore

**Infrastructure & Resilience:** AspNetCore.HealthChecks.NpgSql, AspNetCore.HealthChecks.UI.Client, Microsoft.Extensions.Http.Resilience, Microsoft.Extensions.Hosting.WindowsServices, Microsoft.Extensions.DependencyInjection.Abstractions, Serilog.AspNetCore, Serilog.Enrichers.ClientInfo, Serilog.Sinks.Console

**API & Tooling:** Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore

**Code Generation:** Microsoft.CodeAnalysis.CSharp

**Testing:** xunit.v3, xunit.runner.visualstudio, Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing, Microsoft.NET.Test.Sdk, coverlet.collector
