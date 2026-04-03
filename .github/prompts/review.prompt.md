---
mode: "agent"
description: "Review all unpushed local changes (staged, unstaged, committed) for quality and conventions before pushing"
---

Review all my local changes that haven't been pushed yet. This includes staged, unstaged, and committed-but-not-pushed work.

## Steps

1. Run `git diff` for unstaged changes, `git diff --cached` for staged changes, and `git diff origin/HEAD..HEAD` for committed-but-not-pushed work. Combine all three to get the full picture.

2. Review every changed file against the project conventions in `.github/copilot-instructions.md` and the scoped instructions in `.github/instructions/`. Check for:

### Correctness
- Logic errors, off-by-one, null/undefined risks
- Missing error handling at system boundaries
- Race conditions or async pitfalls (missing await, missing CancellationToken)
- Security issues: injection, hardcoded secrets, missing auth checks

### Conventions
- C#: naming (PascalCase public, _camelCase private), sealed classes, primary constructors, file-scoped namespaces, async/await with CancellationToken, no .Result/.Wait()
- TypeScript: strict mode compliance, no `any`, named exports, camelCase/PascalCase, import order, next-intl for user-facing strings, authenticatedFetch for API calls
- API endpoints: plural nouns, kebab-case, /api/v1/ prefix, OpenAPI metadata (.WithName, .Produces, .WithSummary, .WithTags)
- Logging: structured templates only (no interpolation), PascalCase properties, correct log levels, no secrets logged
- Tests: AAA pattern, MethodName_Scenario_ExpectedResult naming, no mocking owned code, behavior not implementation

### Architecture
- Dependency direction violations (inner layer referencing outer)
- Business logic leaking into endpoints
- Missing service abstractions (logic directly in API layer)

### Packages
- Any NuGet or NPM package not on the approved list

### What to skip
- Do not flag formatting or style in lines that weren't changed
- Do not suggest adding features, abstractions, or tests beyond what the change requires
- Do not flag TODO comments that existed before these changes

## Output format

For each issue found, report:
- **File and line** — exact location
- **Severity** — error (must fix), warning (should fix), nit (optional improvement)
- **What** — one-line description
- **Why** — brief explanation referencing the relevant convention or risk

End with a summary: total errors/warnings/nits and an overall "ready to push" or "fix before pushing" verdict.

If the changes look clean, say so briefly. Don't pad the review with praise.
