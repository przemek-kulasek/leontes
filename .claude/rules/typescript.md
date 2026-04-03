---
paths: "**/*.ts,**/*.tsx"
---

## TypeScript / React Conventions

- TypeScript strict mode
- Functional components only. No class components.
- TailwindCSS for styling. No CSS modules or styled-components.
- camelCase for variables/functions, PascalCase for components
- Import order: React/Next, third-party, local modules, types
- Small, composable components. Extract when a component exceeds ~100 lines.
- next-intl for i18n. All user-facing strings go through translation. Organize message JSON files by feature/page using nested keys (e.g., `chat.emptyState`, `auth.loginButton`, `pricing.monthlyLabel`). Use a `common` namespace for shared strings (buttons, labels, errors) reused across features.
- Prefer named exports over default exports (except pages/layouts)

## Environment Variables

- Only variables prefixed with `NEXT_PUBLIC_` are exposed to the browser — never put secrets there.
- Use `.env.local` for local development overrides (git-ignored). Never commit `.env.local` or `.env` with real values.
- Access env vars via a typed wrapper in `src/lib/env.ts` — never read `process.env` directly in components.
- If a required env var is missing at startup, fail loudly (throw or log a clear error), never silently fall back.

## Error Handling

- Wrap subtrees that may throw unexpected errors in React error boundaries.
- Handle React Query errors inline in the component using the `error` return value — do not use global error handlers for expected API errors.
- Never swallow errors silently (no empty `catch {}` blocks). At minimum log or surface them to the user.
- Map API error responses (RFC 9457 Problem Details) to user-facing messages via i18n strings — never display raw server error text.

## Authenticated API Client

All API calls to protected endpoints must go through a single `authenticatedFetch` wrapper that handles token lifecycle:
- Attach the access token from localStorage to every request via the Authorization header
- On 401 response: attempt a single token refresh, then retry the original request with the new token
- Deduplicate concurrent refresh attempts — if multiple requests fail with 401 at the same time, they must share a single refresh call (use a shared promise), not each trigger their own
- If the refresh itself fails (401, network error): clear tokens, force-logout, redirect to login
- Proactive refresh: before making a request, check if the token expires within 30 seconds and refresh preemptively
- Never import raw `fetch` for API calls in components — always use the authenticated wrapper

## State Management

Use the right tool for the right kind of state:

| State kind | Tool | Example |
|---|---|---|
| Server/remote data | TanStack React Query | User list, product details, paginated results |
| Auth / user session | React Context | Current user, tokens, login status |
| Form input / ephemeral UI | `useState` | Modal open/close, input values, toggles |
| Complex local state with multiple transitions | `useReducer` | Multi-step forms, wizards with back/forward |

Rules:
- Never duplicate server state in Context or useState — React Query is the single source of truth for anything fetched from the API
- Keep Context providers narrow — one provider per concern (auth, theme), not a single global store
- Lift state only as high as it needs to go. If only one component uses it, keep it local
- Do not reach for `useReducer` when a simple `useState` will do — use it only when state transitions are interrelated
