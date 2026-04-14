# 50 Signal Support

## Problem

Leontes currently only accepts messages via the CLI (terminal). The architecture defines two user channels — CLI and Signal — but Signal is unimplemented. Adding Signal support enables E2E encrypted mobile messaging with Leontes, allowing the user to interact from anywhere without a terminal session.

## Prerequisites

- Working POC of API, Worker, and CLI (feature 30)
- API key authentication in place (feature 40)
- Worker running as a Windows Service with `SignalBridgeService` registered

## Rules

- No new NuGet packages required — communication with signal-cli-rest-api is over HTTP, handled by the existing `HttpClientFactory` infrastructure
- signal-cli runs inside Docker via [signal-cli-rest-api](https://github.com/bbernhard/signal-cli-rest-api) — no Java dependency on the host
- Added to the existing `docker-compose.yml` alongside PostgreSQL
- All secrets (phone number) stored in .NET User Secrets — never in committed files
- Signal bridge must not block or interfere with Sentinel services in the Worker
- README must document the full Signal setup from scratch (Docker container, registration, configuration)

## Background: signal-cli-rest-api

[signal-cli-rest-api](https://github.com/bbernhard/signal-cli-rest-api) is a Docker-packaged REST API wrapper around [signal-cli](https://github.com/AsamK/signal-cli). It bundles Java and signal-cli inside the container, exposing a clean HTTP API for sending and receiving Signal messages.

### Why signal-cli-rest-api

| Option | Status | Verdict |
|---|---|---|
| signal-cli-rest-api (Docker) | Actively maintained, REST API, bundles Java | Best fit — no host dependencies, HTTP integration, fits existing Docker Compose setup |
| signal-cli (native JSON-RPC) | Actively maintained | Requires Java 21+ on host — unwanted dependency |
| signald | Archived mid-2023 | Dead |
| libsignal (Rust) | Official, no .NET bindings | Impractical — months of work |
| .NET NuGet packages | All abandoned | Not viable |

### Operating Mode

signal-cli-rest-api runs as a Docker container exposing a REST API on port 8080:

```yaml
# Added to docker-compose.yml
signal:
  image: bbernhard/signal-cli-rest-api:0.98
  ports:
    - "8081:8080"
  volumes:
    - signal-data:/home/.local/share/signal-cli
  environment:
    - MODE=normal
```

The Worker communicates with it via standard HTTP calls — no TCP sockets, no JSON-RPC parsing, no Java on the host.

### Key REST Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/v2/send` | Send a message to a recipient |
| GET | `/v1/receive/{number}` | Poll for incoming messages |
| POST | `/v1/register/{number}` | Start phone number registration |
| POST | `/v1/register/{number}/verify/{code}` | Complete registration with SMS code |
| GET | `/v1/about` | Health check / connection status |
| GET | `/v2/accounts` | List registered accounts |

### Registration

Signal requires a real phone number (prepaid SIM or VoIP). Registration is done via the REST API:

1. `POST /v1/register/+NUMBER` — requests SMS verification
2. Signal sends a verification code via SMS
3. `POST /v1/register/+NUMBER/verify/CODE` — completes registration
4. A CAPTCHA token may be required (passed as request body)

Credentials persist in the Docker volume (`signal-data`), surviving container restarts.

## Solution

### Architecture Overview

```
Phone (Signal app)
    |
    v
Signal servers (E2E encrypted)
    |
    v
signal-cli-rest-api (Docker, REST API on :8081)
    |
    v
Leontes.Worker / SignalBridgeService
    |  polls for incoming messages via GET /v1/receive
    |  forwards to API via HTTP
    v
Leontes.Api (POST /api/v1/messages, Channel: "Signal")
    |  processes via AI agent
    |  streams response via SSE
    v
Leontes.Worker / SignalBridgeService
    |  collects streamed response
    |  sends back via POST /v2/send
    v
signal-cli-rest-api -> Signal servers -> Phone
```

### Components

#### 1. ISignalClient (Application Layer)

Interface in `Leontes.Application/Signal/` defining the contract for Signal communication:

```csharp
public interface ISignalClient
{
    Task<IReadOnlyList<SignalIncomingMessage>> ReceiveMessagesAsync(CancellationToken cancellationToken);
    Task SendMessageAsync(string recipient, string message, CancellationToken cancellationToken);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}
```

`SignalIncomingMessage` is a record in Application:

```csharp
public sealed record SignalIncomingMessage(string Sender, string Content, long Timestamp);
```

#### 2. SignalRestClient (Infrastructure Layer)

Implementation of `ISignalClient` in `Leontes.Infrastructure/Signal/`. Communicates with the signal-cli-rest-api container over HTTP.

Responsibilities:
- Poll `GET /v1/receive/{number}` for incoming messages
- Send messages via `POST /v2/send`
- Health check via `GET /v1/about`
- Uses `HttpClientFactory` with `AddStandardResilienceHandler()` (consistent with other external HTTP clients)
- Log all send/receive activity

REST API usage:

```http
### Poll for incoming messages
GET http://localhost:8081/v1/receive/+1234567890

### Response (array of envelopes)
[
    {
        "envelope": {
            "source": "+0987654321",
            "dataMessage": {
                "message": "Hello Leontes",
                "timestamp": 1234567890
            }
        }
    }
]

### Send a message
POST http://localhost:8081/v2/send
Content-Type: application/json

{
    "message": "Response from Leontes",
    "number": "+1234567890",
    "recipients": ["+0987654321"]
}
```

#### 3. SignalBridgeService (Worker Layer)

Flesh out the existing stub in `Leontes.Worker/Signal/`. This is the orchestrator that bridges Signal messages into the API processing pipeline.

Flow:

1. On startup, verify signal-cli-rest-api is reachable via `ISignalClient.IsAvailableAsync`
2. Poll for incoming messages on a configurable interval (default: 2 seconds)
3. For each incoming message:
   a. Check sender against `AllowedSenders` — skip if not allowed
   b. Forward to API: `POST /api/v1/messages` with `{ "content": "...", "channel": "Signal" }` using Bearer token auth
   c. Read the SSE response stream from the API
   d. Collect all `chunk` events into a complete response
   e. Send the assembled response back to the sender via `ISignalClient.SendMessageAsync`
4. Log all message flow at Information level, errors at Error level

The bridge uses `HttpClientFactory` with resilience for both the signal-cli-rest-api calls and the Leontes API calls.

### Docker Compose

Added to the existing `docker-compose.yml`:

```yaml
signal:
  image: bbernhard/signal-cli-rest-api:0.98
  ports:
    - "8081:8080"
  volumes:
    - signal-data:/home/.local/share/signal-cli
  environment:
    - MODE=normal
  restart: unless-stopped

volumes:
  signal-data:
```

Started alongside PostgreSQL: `docker compose up -d db signal`

### Configuration

Stored in .NET User Secrets for Worker:

```json
{
    "Signal:PhoneNumber": "+1234567890",
    "Signal:AllowedSenders:0": "+1234567890"
}
```

`AllowedSenders` restricts which phone numbers can interact with Leontes. Single-user deployment — this should be the user's personal phone number only.

Default values in `appsettings.json` (non-secret):

```json
{
    "Signal": {
        "BaseUrl": "http://localhost:8081",
        "PollIntervalSeconds": 2
    }
}
```

### Setup Wizard Integration

`leontes init` gains a Signal setup step:

1. Ask if user wants to enable Signal support
2. Check that Docker is running and signal container is up
3. Guide through phone number registration via the REST API
4. Store phone number and allowed senders in Worker User Secrets
5. Test connectivity: send a test message to the user's phone

### README Documentation

The README must include a complete Signal setup guide covering:

1. **Prerequisites** — Docker Desktop running
2. **Start the Signal container** — `docker compose up -d signal`
3. **Register a phone number** — step-by-step curl commands against the REST API, including CAPTCHA handling
4. **Verify registration** — SMS code verification via REST API
5. **Configure Leontes** — exact `dotnet user-secrets set` commands for phone number and allowed senders
6. **Start Leontes Worker** — verifying the bridge connects and polls successfully
7. **Testing** — sending a test message from phone and verifying response
8. **Troubleshooting** — common issues (container not running, port conflict, registration fails, API not reachable)

This is MVP — no installer or automated setup. The README is the UX.

### Message Length Handling

Signal has a 2000-character message limit. For longer AI responses:

- Split at sentence boundaries where possible
- Send as multiple sequential messages with a brief delay between them
- Preserve markdown formatting where Signal supports it (bold, italic, monospace)

### Error Scenarios

| Scenario | Behavior |
|---|---|
| signal-cli-rest-api container not running | Log warning on startup, retry polling with backoff, bridge stays idle |
| signal-cli-rest-api returns errors | Log error, continue polling on next interval |
| API unreachable | Log error, send Signal reply: "I'm having trouble processing your message. Please try again." |
| Message from unknown sender | Log warning, ignore (do not respond) |
| Signal rate limiting | Respect backoff, log warning |

### Security

- Only `AllowedSenders` can interact with Leontes — all other messages are silently ignored
- Phone numbers are stored in User Secrets, never committed
- signal-cli handles E2E encryption inside the container — Leontes never touches Signal protocol directly
- Communication between Worker and signal-cli-rest-api is localhost HTTP only

## Out of Scope

- Group chat support (single-user, 1:1 only for now)
- Signal attachments (images, files) — text only
- Signal reactions, read receipts, typing indicators
- Multiple Signal accounts
- Signal Desktop linking (separate concern from bot registration)
