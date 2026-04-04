# 20 — Long-Term Memory

## Problem

Without persistent memory, the assistant repeats questions, forgets preferences, and loses context between sessions.

## Acceptance Criteria

- [ ] Past conversations are embedded and stored in pgvector
- [ ] Processing loop retrieves relevant memory before each LLM call
- [ ] User can ask "what do you remember about X" and get accurate recall

## API Contract

```
GET /api/v1/memory/search?q=string&limit=5
Response: [ { "id": "guid", "content": "string", "relevance": 0.87, "sourceMessageId": "guid" } ]
```

## Data Model

- `MemoryEntry`: Id, Content, Embedding (vector), SourceMessageId, CreatedAt
- pgvector index on Embedding column

## Dependencies

- `10 — CLI Chat` (needs messages to exist before they can be memorized)

## Open Questions

- Embedding model: local (e.g., all-MiniLM) or API-based?
- Chunking strategy for long conversations?
