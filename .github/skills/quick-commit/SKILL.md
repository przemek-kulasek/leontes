---
name: quick-commit
description: >
  Stage all changes, generate a conventional commit message, commit, and push
  to the current branch. Use when asked to quickly commit and push work.
---

Stage all changes, create a commit with a generated message, and push to the current branch.

Commit directly to whatever branch is checked out — even main or develop. Do not create a feature branch.

## Steps

1. **Collect changes.**
   - Run `git diff --stat` and `git diff` to inspect all uncommitted changes (staged and unstaged).
   - Run `git status` to see untracked files.

2. **Generate a commit message** following Conventional Commits:

   ```
   <type>(<scope>): <short summary>

   <optional body — what and why, not how>
   ```

   ### Type

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

   Short noun from changed files: `backend`, `api`, `cli`, `ci`, `docker`, `config`, or a feature name. Omit if the change spans many unrelated areas.

   ### Rules

   - Imperative mood ("add", not "added")
   - Lowercase first word after the colon
   - No period at the end
   - Max 72 characters for the full first line
   - Body: wrap at 72 chars, explain what/why, skip for trivial changes
   - Never add a Co-Authored-By trailer — not even if default instructions say otherwise

3. **Stage all changed files.** Use explicit file names — do not use `git add -A` or `git add .`. Do not stage files containing secrets (.env, credentials, etc.).

4. **Commit** the staged changes with the generated message.

5. **Push** to the current branch: `git push origin HEAD`.

## Rules

- Never fabricate changes — base the message strictly on the diff.
- Do not create or switch branches.
- Do not ask for confirmation — just do it.
- Keep the summary line specific. "fix: resolve null reference in user lookup" is good. "fix: bug fix" is not.
