---
applyTo: "**/*.cs"
---

## C# / .NET Conventions

### Naming
- PascalCase for public members, properties, methods, and classes
- `_camelCase` for private fields (e.g., `_userRepository`)
- Interfaces prefixed with `I` (e.g., `IUserService`)

### Language & Style
- `async`/`await` throughout — never `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`
- Always pass `CancellationToken` through async methods; never ignore it
- Nullable reference types enabled (`<Nullable>enable</Nullable>`) — handle nulls explicitly, no `!` suppression without a comment explaining why
- Use `var` when the type is obvious from the right-hand side; use explicit types when it adds clarity
- File-scoped namespaces (`namespace Foo.Bar;`) in every file
- One class per file; filename matches the class name
- Prefer `sealed` for classes not designed for inheritance
- Use primary constructors (C# 12+) for simple dependency injection
- Records for DTOs, value objects, and any immutable data structures

### Program.cs — Keep It Clean
`Program.cs` must only wire up the application. It should contain nothing except:
1. `builder` setup (configuration, logging)
2. Calls to extension methods that register services (`builder.Services.AddApplication()`, `builder.Services.AddInfrastructure()`)
3. Middleware pipeline configuration (`app.UseX()`)
4. `app.Run()`

Move all service registrations to `AddX(this IServiceCollection services)` extension methods in the relevant project layer. Move all pipeline/middleware config to `UseX(this WebApplication app)` extension methods. Never inline registrations in `Program.cs`.

### Dependency Injection
- Constructor injection only. No service locator (`IServiceProvider` injected into business logic).
- Register services in the layer that owns them via the corresponding extension method.

### Architecture
- Inner layers never reference outer layers — Domain has zero project references
- Minimal API endpoints grouped by feature in an `Endpoints/` folder
- API errors use ProblemDetails (RFC 9457) via the global exception handler — do not return raw exception messages

### Miscellaneous
- Use `throw` (not `return null`) for exceptional conditions — let the global handler format the response
- No `static` classes for business logic — they are untestable and hide dependencies
- Global usings go in a dedicated `GlobalUsings.cs` file per project, not scattered across files
