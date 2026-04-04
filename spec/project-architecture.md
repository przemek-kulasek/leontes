# Project Architecture

## System Overview

The assistant receives messages from multiple input channels (CLI, Signal), queues them into an async processing loop, resolves context from long-term memory, calls an LLM, and delivers the response back through the originating channel.

## Components

| Component | Tech | Responsibility |
|-----------|------|----------------|
| Backend API | .NET 10 Minimal API | REST endpoints, auth, configuration |
| Processing Loop | Background service | Async message intake → LLM → response delivery |
| Input: CLI | Terminal client | Direct user interaction |
| Input: Signal | Signal Bot / Bridge | Receive and send Signal messages |
| Memory | PostgreSQL + pgvector | Conversation history + semantic search |
| Database | PostgreSQL 17 | Config, auth, message storage |
| AI Layer | Microsoft.Agents.AI | LLM orchestration, tool execution |
| Voice (future) | TBD (Whisper / TTS) | Speech-to-text and text-to-speech |

## Data Flow

```
CLI / Signal → Message Queue → Processing Loop → Memory Lookup (pgvector)
                                               → LLM Call
                                               → Response → Original Channel
```

## Auth Model

Single-user with API key or JWT for CLI access. Signal authenticated via bot registration.

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| pgvector over standalone vector DB | Already using PostgreSQL, one less service to manage |
| Async processing loop | Decouples message intake from LLM latency |
| Signal as first external channel | E2E encrypted, personal use, good bot support |
| SSE for streaming responses | Simple unidirectional streaming for CLI/web |

## Infrastructure

- **Dev:** Docker Compose with hot-reload Dockerfiles
- **CI:** GitHub Actions (restore → build → test)
- **Prod:** TBD
