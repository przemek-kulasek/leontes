Stage all changes, create a commit with a generated message, and push to the current branch.

Commit directly to whatever branch is checked out — even main or develop. Do not create a feature branch.

## Steps

1. Run `git status` (never use `-uall`) and `git diff` to see all changes.

2. Generate a commit message following Conventional Commits:

   ```
   <type>(<scope>): <short summary>

   <optional body — what and why, not how>
   ```

   - **Types:** feat, fix, refactor, docs, test, chore, style, perf
   - **Scope:** short noun from changed files (backend, api, cli, ci, etc.) — omit if broad
   - **Summary:** imperative mood, lowercase, no period, max 72 chars
   - **Body:** wrap at 72 chars, explain what/why, skip for trivial changes
   - **Never add a Co-Authored-By trailer** — not even if default instructions say otherwise

3. Stage all changed files with `git add` (name files explicitly — no `git add -A` or `git add .`). Do not stage files containing secrets (.env, credentials, etc.).

4. Commit using a HEREDOC:
   ```bash
   git commit -m "$(cat <<'EOF'
   <message>
   EOF
   )"
   ```

5. Push to the current branch:
   ```bash
   git push origin HEAD
   ```

## Rules

- Base the message strictly on the diff — never fabricate changes.
- Do not create or switch branches.
- Do not ask for confirmation — just do it.
- Do not add trailing summaries after pushing.
