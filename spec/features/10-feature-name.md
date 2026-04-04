# 10 — CLI Chat

## Problem

The user needs a direct way to chat with the assistant from a terminal without any external dependencies.

## Acceptance Criteria

- [ ] User can send a message and receive a streamed response
- [ ] Conversation context is preserved within a session
- [ ] Messages are persisted to the database for memory

## API Contract

```
POST /api/v1/messages
Request:  { "content": "string", "channel": "cli" }
Response: SSE stream of assistant response chunks

GET /api/v1/messages?limit=20
Response: [ { "id": "guid", "role": "user|assistant", "content": "string", "createdAt": "datetime" } ]
```

## Data Model

- `Message`: Id, Role, Content, Channel, ConversationId, CreatedAt
- `Conversation`: Id, Title, CreatedAt, LastMessageAt

## Dependencies

`None` — this is the foundation.

## Open Questions

- CLI client as a separate dotnet tool or integrated into the API project?
