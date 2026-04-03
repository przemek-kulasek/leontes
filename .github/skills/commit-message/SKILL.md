---
name: commit-message
description: >
  Generate a conventional commit message from staged (or all uncommitted) changes.
  Use this skill when asked to write, draft, or prepare a commit message.
---

Generate a commit message for the changes that are about to be committed.

## Steps

1. **Collect the diff.**
   - Run `git diff --cached --stat` and `git diff --cached` to inspect staged changes.
   - If nothing is staged, fall back to `git diff --stat` and `git diff` for unstaged changes and let the user know.

2. **Understand the change.**
   - Identify which files changed, what was added/removed/modified.
   - Determine the *intent* — is this a new feature, a bug fix, a refactor, documentation, tests, CI config, dependency update, etc.?

3. **Write the commit message** following the Conventional Commits format:

   ```
   <type>(<scope>): <short summary>

   <optional body — what and why, not how>

   Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
   ```

   ### Type

   Use one of:

   | Type       | When to use                                      |
   |------------|--------------------------------------------------|
   | `feat`     | A new feature or user-visible capability          |
   | `fix`      | A bug fix                                         |
   | `refactor` | Code restructuring with no behavior change        |
   | `docs`     | Documentation only                                |
   | `test`     | Adding or updating tests                          |
   | `chore`    | Build, CI, tooling, dependencies, config changes  |
   | `style`    | Formatting, whitespace — no logic change          |
   | `perf`     | Performance improvement                           |

   ### Scope

   A short noun identifying the area of the codebase, derived from the changed files:

   - `backend`, `frontend`, `api`, `auth`, `db`, `infra`, `ci`, `docker`, `i18n`, `config`, or a feature name.
   - Omit scope if the change spans many unrelated areas.

   ### Summary line rules

   - Imperative mood ("add", not "added" or "adds")
   - Lowercase first word (after the colon)
   - No period at the end
   - Max 72 characters for the full first line

   ### Body rules

   - Wrap at 72 characters
   - Explain *what* changed and *why* — the diff already shows *how*
   - Skip the body for trivial, self-explanatory changes
   - If the change addresses a GitHub issue, add `Closes #<number>` or `Refs #<number>`

4. **Present the message.**
   - Show the complete commit message in a fenced code block.
   - If the diff is large or touches multiple concerns, suggest splitting into multiple commits and provide a message for each.

## Rules

- Never fabricate changes — base the message strictly on the diff output.
- Keep the summary line specific. "fix: resolve null reference in user lookup by email" is good. "fix: bug fix" is not.
- When both backend and frontend change in the same diff, prefer two separate commit messages (one per layer) unless they are tightly coupled.
- Always include the `Co-authored-by` trailer.
