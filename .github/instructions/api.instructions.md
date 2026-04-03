---
applyTo: "**/*Endpoints*,**/*Endpoint*,**/Endpoints/**/*"
---

## RESTful API Conventions

### URL Design
- Use plural nouns for resource names: `/api/v1/users`, `/api/v1/orders`
- Use kebab-case for multi-word resources: `/api/v1/order-items`, `/api/v1/user-profiles`
- Nest resources to express ownership: `/api/v1/users/{userId}/orders`
- Maximum nesting depth: 2 levels. Beyond that, promote the sub-resource to a top-level resource with a filter parameter
- No verbs in URLs — let HTTP methods express the action
- All endpoints are prefixed with `/api/v1/`

### HTTP Verbs

| Verb | Purpose | Response | Idempotent |
|---|---|---|---|
| GET | Retrieve a resource or collection | 200 with body | Yes |
| POST | Create a new resource | 201 with Location header + created resource | No |
| PUT | Full replacement of a resource | 200 with updated resource or 204 | Yes |
| PATCH | Partial update of a resource | 200 with updated resource | No |
| DELETE | Remove a resource | 204 No Content | Yes |

### Versioning
- Version in the URL path: `/api/v1/`, `/api/v2/`
- When a breaking change is needed, increment the version and keep the old endpoint alive until consumers have migrated
- Non-breaking additions (new optional fields, new endpoints) do not require a version bump

### Pagination
Endpoints that return collections which could grow large must support pagination. Use a consistent request/response shape:

Request query parameters:
- `page` (int, default 1) — 1-based page number
- `pageSize` (int, default 20, max 100)
- `sortBy` (string, optional) — property name
- `sortDirection` (string, optional) — `asc` or `desc`

Response envelope:
```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 142,
  "totalPages": 8
}
```

Not every list endpoint needs pagination — small, bounded collections (e.g., roles, categories, enum lookups) can return a plain array.

### Filtering & Search
- Use query parameters for simple filters: `GET /api/v1/users?role=admin&status=active`
- Use a `search` query parameter for free-text search: `GET /api/v1/products?search=widget`

### Response Conventions
- Return the created/updated resource in the response body for POST, PUT, PATCH — the client should not need to make a follow-up GET
- Return 204 No Content for DELETE — no body
- Use RFC 9457 Problem Details for all error responses (handled by the global exception handler)
- Do not wrap successful responses in a `{ "data": ..., "success": true }` envelope — return the resource directly

### API Documentation
- Use Scalar as the API documentation UI, mapped to `/scalar` — enabled in development only, not exposed in production
- All endpoints must include OpenAPI metadata: summary, response types, and parameter descriptions via `.WithName()`, `.Produces<T>()`, `.WithSummary()`, `.WithTags()`
- Group endpoints with `.WithTags()` matching the feature name (e.g., "Users", "Chat", "Billing")

### Endpoint Organization
- Group endpoints by feature in the `Endpoints/` folder (e.g., `Endpoints/Users.cs`, `Endpoints/Orders.cs`)
- Each file registers its routes via a static `MapXxxEndpoints(this WebApplication app)` extension method
- Keep endpoint methods short — delegate business logic to services, not inline in the lambda
