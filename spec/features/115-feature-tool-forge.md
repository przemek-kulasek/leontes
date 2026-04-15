# 115 — Tool Forge

## Problem

The assistant's capabilities are fixed at compile time. When it encounters a task it can't handle — converting a file format, querying a specific API, parsing a proprietary log format — it can only apologize. A human developer would write a script to solve the problem and reuse it next time. The assistant needs the same ability: detect a capability gap, write a tool, test it, get approval, and add it to its catalog.

## Prerequisites

- Working API with tool-calling support via Microsoft.Agents.AI (feature 30)
- Hierarchical Memory with Synapse Graph (feature 80) — tools are tracked as entities, usage is recorded for pruning
- Thinking Pipeline (feature 70) — tool generation is triggered in the Execute stage

## Rules

- Dynamic code compilation uses `Microsoft.CodeAnalysis.CSharp` (Roslyn) — approved and version-pinned
- User approval uses `ApprovalRequiredAIFunction` from `Microsoft.Agents.AI.Workflows` — no custom approval flow
- No code runs without explicit user approval — the assistant proposes the tool, the user reviews and approves via the Proactive Communication channel (feature 65)
- Tool code runs in a restricted Roslyn scripting environment with a namespace allowlist — no file system access, no network access, no reflection unless explicitly allowed
- Tools are pure functions: input → output. No side effects, no state mutation, no database access
- Each tool must have a unit test written by the agent and verified before approval
- Tool metadata (name, description, parameters) must be clear and human-readable
- Unused tools are pruned automatically — tracked via Synapse Graph (feature 80)
- Maximum tool code size: 500 lines (if it's bigger, it's not a tool — it's a feature)
- Tools are persisted as source code in the database, not as compiled assemblies

## Background

### Voyager (Wang et al., 2023) — Skill Library

The Voyager paper demonstrates an open-ended agent that builds a progressively expanding "Skill Library." Key principles adopted:
- Tools are described in natural language alongside their code
- Tools are verified via automated testing before registration
- The agent queries its library before attempting to write a new tool
- Unused tools are pruned to keep the library focused

### Scientific Method Loop

```
1. HYPOTHESIS  — "I need a tool to convert CSV to JSON"
2. SYNTHESIS   — Generate C# code using Roslyn
3. TEST        — Write and run a unit test
4. REVIEW      — Present code + test results to user
5. REGISTER    — On approval, add to catalog
6. TRACK       — Record usage in Synapse Graph, prune if unused
```

This ensures no unreviewed code enters the system and tools are validated before use.

### Roslyn Scripting vs. Full Compilation

| Approach | Pros | Cons |
|---|---|---|
| Full Roslyn compilation to assembly | Full .NET, fast execution | Hard to sandbox, DLL management, security risk |
| Roslyn scripting (CSharpScript) | Built-in sandbox, configurable imports, eval model | Slightly slower, limited to script semantics |
| Docker container | Strong isolation | Heavy, slow startup, infrastructure complexity |
| WebAssembly (Wasm) | Strong sandbox | Requires unapproved packages |

**Decision:** Roslyn scripting with namespace allowlist. It's the simplest approach that provides adequate isolation for a single-user, local deployment. The allowlist restricts what the tool code can access.

## Solution

### Architecture Overview

```
Agent detects capability gap
    |
    v
[Check Tool Catalog] — does a matching tool already exist?
    |  yes → use it          no → proceed to generation
    v
[Generate Tool Code] — LLM writes a C# function + metadata
    |
    v
[Generate Unit Test] — LLM writes a test for the tool
    |
    v
[Compile & Run Test] — Roslyn compiles and executes in sandbox
    |  fail → LLM fixes code (max 3 attempts)
    v
[Present to User] — ApprovalRequiredAIFunction wraps the tool,
    |                emits ToolApprovalRequestContent via RequestInfoEvent (feature 65)
    |  rejected → discard
    v
[Register in Catalog] — store source code + metadata in DB
    |
    v
[Track Usage] — Synapse Graph records each invocation
    |
    v
[Auto-Prune] — remove tools unused for 30+ days
```

#### Tool Approval via Agent Framework

User approval for newly forged tools leverages `ApprovalRequiredAIFunction` from `Microsoft.Agents.AI.Workflows`. This wraps any `AIFunction` so that the first time it's called, it emits a `ToolApprovalRequestContent` inside a `RequestInfoEvent`. The Proactive Communication infrastructure (feature 65) delivers this to the user via whichever channel is active.

```csharp
// Wrapping a forged tool for approval
var forgedFunction = new ForgedAIFunction(tool.Name, tool.Description, executor);
var approvalWrapped = new ApprovalRequiredAIFunction(forgedFunction);

// When the agent calls this tool:
// 1. Framework emits RequestInfoEvent with ToolApprovalRequestContent
// 2. IWorkflowEventBridge delivers to CLI/Signal/Telegram (feature 65)
// 3. User sees: tool name, description, arguments, source code
// 4. User approves/rejects via POST /api/v1/stream/respond
// 5. On approve: tool executes and result returns to agent
// 6. On reject: agent receives rejection, tries alternative approach
```

This replaces a custom approval flow — the framework handles correlation, timeout, and checkpoint persistence of pending approvals. Approval timeout defaults to 30 minutes with deny-by-default behavior — see feature 85 (Error Recovery & Resilience) for the `RequestPortOptions` configuration and timeout semantics.

### Data Model

#### ForgedTool (Domain Layer)

```csharp
public sealed class ForgedTool : Entity
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string SourceCode { get; set; }
    public required string TestCode { get; set; }
    public required string InputSchema { get; set; }
    public required string OutputSchema { get; set; }
    public ForgedToolStatus Status { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastError { get; set; }
}

public enum ForgedToolStatus
{
    Draft,
    Approved,
    Disabled,
    Pruned
}
```

### Components

#### 1. IToolCatalog (Application Layer)

```csharp
public interface IToolCatalog
{
    Task<ForgedTool?> FindByNameAsync(string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<ForgedTool>> SearchAsync(
        string query,
        CancellationToken cancellationToken);

    Task<ForgedTool> RegisterAsync(
        string name,
        string description,
        string sourceCode,
        string testCode,
        string inputSchema,
        string outputSchema,
        CancellationToken cancellationToken);

    Task RecordUsageAsync(Guid toolId, CancellationToken cancellationToken);

    Task PruneUnusedAsync(TimeSpan unusedThreshold, CancellationToken cancellationToken);
}
```

#### 2. IToolCompiler (Application Layer)

```csharp
public interface IToolCompiler
{
    Task<ToolCompilationResult> CompileAndTestAsync(
        string sourceCode,
        string testCode,
        CancellationToken cancellationToken);
}

public sealed record ToolCompilationResult(
    bool Success,
    string? Output,
    string? Error,
    TimeSpan CompilationTime,
    TimeSpan? ExecutionTime);
```

#### 3. RoslynToolCompiler (Infrastructure Layer)

```csharp
public sealed class RoslynToolCompiler(
    ILogger<RoslynToolCompiler> logger) : IToolCompiler
{
    // Allowed namespaces — tools can only use these
    private static readonly IReadOnlySet<string> AllowedNamespaces = new HashSet<string>
    {
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text",
        "System.Text.Json",
        "System.Text.RegularExpressions",
        "System.Globalization",
        "System.IO" // StringReader/StringWriter only, no file access
    };

    public async Task<ToolCompilationResult> CompileAndTestAsync(
        string sourceCode,
        string testCode,
        CancellationToken cancellationToken)
    {
        // 1. Parse source code
        var sourceTree = CSharpSyntaxTree.ParseText(sourceCode);

        // 2. Validate namespace usage against allowlist
        var usedNamespaces = ExtractUsedNamespaces(sourceTree);
        var forbidden = usedNamespaces.Except(AllowedNamespaces).ToList();
        if (forbidden.Count > 0)
            return Fail($"Forbidden namespaces: {string.Join(", ", forbidden)}");

        // 3. Compile with Roslyn
        var compilation = CSharpCompilation.Create("ToolAssembly")
            .AddSyntaxTrees(sourceTree, CSharpSyntaxTree.ParseText(testCode))
            .AddReferences(GetSafeReferences())
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // 4. Check for compilation errors
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        if (diagnostics.Count > 0)
            return Fail(string.Join("\n", diagnostics.Select(d => d.GetMessage())));

        // 5. Emit and load assembly in restricted context
        // 6. Run the test method
        // 7. Return results
    }
}
```

#### 4. ToolForgeService (Infrastructure Layer)

Orchestrates the generation-compile-test-approve loop:

```csharp
public sealed class ToolForgeService(
    IChatClient chatClient,
    IToolCompiler compiler,
    IToolCatalog catalog,
    ILogger<ToolForgeService> logger)
{
    public async Task<ToolForgeResult> ForgeToolAsync(
        string capability,
        CancellationToken cancellationToken)
    {
        // 1. Check catalog for existing tool
        var existing = await catalog.SearchAsync(capability, cancellationToken);
        if (existing.Count > 0)
            return ToolForgeResult.ExistingTool(existing[0]);

        // 2. Generate tool code via LLM
        var generation = await GenerateToolCodeAsync(capability, cancellationToken);

        // 3. Compile and test (max 3 attempts with LLM fixes)
        var result = await CompileWithRetriesAsync(
            generation, maxAttempts: 3, cancellationToken);

        if (!result.Success)
            return ToolForgeResult.Failed(result.Error!);

        // 4. Return for user approval (not auto-registered)
        return ToolForgeResult.PendingApproval(generation);
    }
}
```

#### 5. Tool Execution at Runtime

Approved tools are loaded and executed via Roslyn scripting when the agent decides to use them:

```csharp
public sealed class ForgedToolExecutor(
    IToolCatalog catalog,
    ILogger<ForgedToolExecutor> logger)
{
    public async Task<string> ExecuteAsync(
        string toolName,
        string inputJson,
        CancellationToken cancellationToken)
    {
        var tool = await catalog.FindByNameAsync(toolName, cancellationToken);
        if (tool is null || tool.Status != ForgedToolStatus.Approved)
            throw new NotFoundException($"Tool '{toolName}' not found or not approved");

        // Execute via CSharpScript.EvaluateAsync with restricted globals
        var result = await CSharpScript.EvaluateAsync<string>(
            tool.SourceCode + $"\nreturn Execute({inputJson});",
            ScriptOptions.Default
                .WithReferences(GetSafeReferences())
                .WithImports(AllowedNamespaces),
            cancellationToken: cancellationToken);

        await catalog.RecordUsageAsync(tool.Id, cancellationToken);
        return result;
    }
}
```

### LLM Prompt for Tool Generation

```
You are a tool author. Write a C# tool with the following requirements:

CAPABILITY: {userDescription}

CONSTRAINTS:
- The tool must be a single static class with a static Execute method
- Input and output must be JSON-serializable
- Only these namespaces are allowed: System, System.Collections.Generic, System.Linq, System.Text, System.Text.Json, System.Text.RegularExpressions, System.Globalization
- No file I/O, no network calls, no reflection, no unsafe code
- Maximum 500 lines

OUTPUT FORMAT:
1. Tool source code (```csharp block)
2. Test code (```csharp block with a static Test method that returns bool)
3. Tool metadata:
   - Name: short-kebab-case
   - Description: one sentence
   - Input schema: JSON object description
   - Output schema: JSON object description
```

### API Contract

```http
### List approved tools
GET /api/v1/tools?status=Approved
Authorization: Bearer {token}

### Response
[
    {
        "id": "guid",
        "name": "csv-to-json",
        "description": "Converts CSV text to a JSON array of objects",
        "usageCount": 12,
        "lastUsedAt": "2026-04-12T10:00:00Z",
        "status": "Approved"
    }
]
```

```http
### Review pending tool (get source code + test results)
GET /api/v1/tools/{toolId}
Authorization: Bearer {token}

### Response
{
    "id": "guid",
    "name": "csv-to-json",
    "description": "Converts CSV text to a JSON array of objects",
    "sourceCode": "public static class CsvToJson { ... }",
    "testCode": "public static class CsvToJsonTest { ... }",
    "status": "Draft",
    "inputSchema": "{ \"csv\": \"string\" }",
    "outputSchema": "{ \"json\": \"string\" }"
}
```

```http
### Approve or reject a tool
PATCH /api/v1/tools/{toolId}
Authorization: Bearer {token}
Content-Type: application/json

{ "status": "Approved" }
```

### Auto-Pruning

A background service checks tool usage periodically:

```csharp
public sealed class ToolPruningService(
    IToolCatalog catalog,
    ILogger<ToolPruningService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            await catalog.PruneUnusedAsync(
                unusedThreshold: TimeSpan.FromDays(30),
                stoppingToken);

            logger.LogInformation("Tool pruning completed");
        }
    }
}
```

Pruned tools are set to `ForgedToolStatus.Pruned` (soft delete) and excluded from catalog searches. They can be restored if needed.

### Synapse Graph Integration

When a tool is registered, it's added to the Synapse Graph:

```
(Tool: "csv-to-json") -[CREATED_FOR]-> (Conversation: "data processing chat")
(Tool: "csv-to-json") -[USED_BY]-> (Conversation: "monthly report generation")
```

This allows the memory system to suggest relevant tools based on context — "you used csv-to-json last time you worked with spreadsheet data."

### Configuration

```json
{
    "ToolForge": {
        "Enabled": true,
        "MaxSourceLines": 500,
        "MaxCompilationAttempts": 3,
        "CompilationTimeoutSeconds": 30,
        "PruneAfterDays": 30,
        "AllowedNamespaces": [
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Text.Json",
            "System.Text.RegularExpressions",
            "System.Globalization"
        ]
    }
}
```

### Error Handling

| Scenario | Behavior |
|---|---|
| LLM generates code that won't compile | Fix attempt (max 3), then report failure to user |
| LLM generates code using forbidden namespace | Reject immediately with clear error |
| Test fails | Fix attempt (max 3), then report failure with test output |
| Tool execution exceeds timeout (30s) | Kill execution, return error |
| Tool throws at runtime | Catch, log, increment error counter, disable tool after 3 consecutive failures |
| User rejects proposed tool | Discard draft, log the rejection for future reference |

### Security

- **Namespace allowlist is enforced at compile time** — forbidden `using` statements prevent compilation
- **No file system access** — `System.IO` is allowed only for `StringReader`/`StringWriter` (validated via syntax tree analysis)
- **No network access** — `System.Net` and `System.Net.Http` are not in the allowlist
- **No reflection** — `System.Reflection` is not in the allowlist
- **No `unsafe` code** — compilation option `AllowUnsafe = false`
- **Execution timeout** — CancellationToken with 30-second timeout prevents infinite loops
- **All code reviewed by user** — no auto-registration, no auto-execution of unapproved tools

## Acceptance Criteria

- [ ] Agent detects capability gap and proposes a new tool with source code and test
- [ ] Roslyn compiles tool code with namespace allowlist enforcement
- [ ] Auto-generated unit test runs and passes before tool is presented for approval
- [ ] User can review source code, test results, and approve/reject via `ApprovalRequiredAIFunction` → `RequestInfoEvent` → Proactive Communication (feature 65)
- [ ] Approved tools are callable by the agent during normal conversation
- [ ] Tool usage is tracked in the database (usage count, last used timestamp)
- [ ] Tools unused for 30+ days are automatically pruned (soft delete)
- [ ] Tool fails gracefully at runtime (caught, logged, reported to user)
- [ ] Forbidden namespaces are rejected at compile time
- [ ] Maximum 3 compilation retry attempts before reporting failure
- [ ] Tool source code is stored in the database, not as compiled DLLs

## Out of Scope

- Tool marketplace or sharing between Leontes instances
- Tools with side effects (file I/O, network calls, database access)
- Tools that require external dependencies (NuGet packages)
- Visual tool editor / IDE integration
- Tool versioning (edit creates a new tool, doesn't version the old one)
- Docker-based sandbox (deferred — Roslyn scripting is sufficient for now)
- Automatic tool generation without user prompting (agent must detect a gap, not speculatively create tools)
