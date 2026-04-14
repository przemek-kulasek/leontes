# 60 Telegram Support

## Problem

Signal requires a dedicated SIM card and its HTTP/CLI tooling (signal-cli) is community-maintained, not officially supported by Signal. Telegram offers an official, stable Bot API over HTTPS with no extra containers or phone-number registration — a lighter, more reliable second mobile channel.

Adding Telegram alongside Signal also forces a proper **messaging channel abstraction** so that all channels (CLI, Signal, Telegram, and any future ones) share a common contract. Today Signal is wired in directly with channel-specific types; a second channel is the right time to generalize.

## Prerequisites

- Working Signal bridge (feature 50) — the existing implementation is the reference for how a channel bridge works
- API key authentication in place (feature 40)
- Worker running as a Windows Service

## Rules

- No new NuGet packages — the Telegram Bot API is plain HTTPS/JSON, handled by the existing `HttpClientFactory` + resilience infrastructure
- No Docker container needed — Telegram's API is cloud-hosted at `https://api.telegram.org`
- All secrets (bot token, allowed chat IDs) stored in .NET User Secrets — never in committed files
- Telegram bridge must not block or interfere with Sentinel or Signal services in the Worker
- README must document Telegram setup (bot creation, configuration, testing)
- The channel abstraction introduced here must be backward-compatible — Signal keeps working exactly as before

## Background: Telegram Bot API

The [Telegram Bot API](https://core.telegram.org/bots/api) is an official, first-party HTTPS API for building bots. Bots are created via [@BotFather](https://t.me/botfather) and communicate through Telegram's cloud servers.

### Why Telegram

| Criterion | Signal | Telegram |
|---|---|---|
| SIM card required | Yes — dedicated number needed | No — bot token only |
| API status | Community (signal-cli) | Official, first-party |
| Extra infrastructure | Docker container (signal-cli-rest-api) | None — cloud API |
| E2E encryption | Always on | Optional (Secret Chats — not available for bots) |
| Transport encryption | Yes (Signal Protocol) | Yes (MTProto, TLS) |
| Message limit | 2,000 characters | 4,096 characters |
| Markdown support | Limited (bold, italic, mono) | Full (MarkdownV2: bold, italic, mono, code blocks, links, spoilers) |
| File/media support | Future | Future |

### Security Considerations

Telegram standard chats are **not E2E encrypted** — messages are encrypted in transit (TLS + MTProto) and at rest on Telegram's servers, but Telegram can technically read them. For users who require E2E encryption, Signal remains the primary channel. Telegram is the pragmatic channel — easier to set up, official API, no SIM card, good enough security for most use cases.

The spec assumes the user makes an informed choice about which channel to use for which conversations.

### Communication Mode

Two modes are available for receiving messages:

| Mode | Mechanism | Latency | Complexity |
|---|---|---|---|
| **Long polling** (MVP) | `getUpdates` with 30s timeout | ~instant (connection hangs until message arrives) | Simple — outbound HTTPS only, no public URL needed |
| Webhook | Telegram pushes to a public HTTPS endpoint | ~instant | Requires public URL + TLS cert + port forwarding |

**MVP uses long polling.** It requires no public URL, no TLS certificate, and no port forwarding — ideal for a self-hosted local deployment. The connection stays open until Telegram has something to deliver, so it's effectively push-based despite being a pull API. Webhook mode can be added later if Leontes ever moves to a cloud-hosted deployment.

### Bot Creation

1. Open Telegram, search for **@BotFather**
2. Send `/newbot`, follow prompts (choose name and username)
3. BotFather returns a **bot token** — store it securely, it grants full control of the bot

No phone number, no SIM card, no CAPTCHA, no Docker container.

## Solution

### Channel Abstraction

Before adding Telegram, introduce a common messaging channel contract so all bridges share the same pattern. This lives in the **Application** layer.

The abstraction provides:
- **IMessagingClient** — a channel-agnostic interface for receiving messages, sending messages, and checking availability. Each channel implementation (Signal, Telegram) identifies itself via a `Channel` property.
- **IncomingMessage** — a shared record replacing channel-specific DTOs like `SignalIncomingMessage`. Includes a `Channel` field so downstream code knows the origin.
- **MessageSplitter** — shared utility for splitting long AI responses at sentence boundaries, parameterized by max length (Signal: 2000, Telegram: 4096).

**Migration path:** `SignalRestClient` adopts the new interface. `SignalBridgeService` switches to `IMessagingClient`. Then `TelegramBotClient` implements the same interface, and `TelegramBridgeService` follows the same bridge pattern.

The bridge services remain separate classes — Signal polls a local Docker container on a fixed interval, Telegram long-polls a cloud API with a 30s timeout. Different enough that a shared base class would be a premature abstraction. The shared contract is the interface.

### Architecture

```
Phone (Telegram app)
    |
    v
Telegram cloud servers (MTProto + TLS)
    |
    v
Telegram Bot API (api.telegram.org)
    |
    v
Leontes.Worker / TelegramBridgeService
    |  long-polls for incoming messages
    |  forwards to API via HTTP
    v
Leontes.Api (POST /api/v1/messages, Channel: "Telegram")
    |  processes via AI agent
    |  streams response via SSE
    v
Leontes.Worker / TelegramBridgeService
    |  collects streamed response
    |  sends back via Telegram Bot API
    v
Telegram servers -> Phone
```

### Telegram Client (Infrastructure Layer)

Implements the shared `IMessagingClient` interface. Communicates with the Telegram Bot API over HTTPS using `HttpClientFactory` with standard resilience.

Responsibilities:
- Long-poll for incoming messages with configurable timeout (default 30s)
- Send text messages back to the originating chat
- Verify bot token on startup via the `getMe` endpoint
- Track update offset to acknowledge processed messages and prevent re-delivery
- Disable any stale webhook on startup to ensure polling mode is active

### Telegram Bridge Service (Worker Layer)

New `BackgroundService` in the Worker, following the same pattern as `SignalBridgeService`.

Flow:
1. Check if bot token is configured — if empty, log that the bridge is disabled and exit
2. Verify the token is valid — if not (401), log error and exit (don't retry a bad token)
3. Disable any existing webhook
4. Long-poll for incoming updates in a loop
5. For each text message: check chat ID against allowed list, forward to API, collect SSE response, send reply back
6. Track offset so Telegram doesn't re-deliver processed messages

### MessageChannel Enum

Add `Telegram` to the existing `MessageChannel` enum alongside `Cli` and `Signal`.

### Configuration

**Secrets (User Secrets for Worker):**
- `Telegram:BotToken` — the token from @BotFather. If empty/missing, bridge is disabled.
- `Telegram:AllowedChatIds` — list of Telegram chat IDs permitted to interact. If empty, all messages are rejected.

**Non-secret defaults (appsettings.json):**
- `Telegram:PollTimeoutSeconds` — long-poll timeout, default 30 (Telegram's recommended max)

No `BaseUrl` setting — the Telegram Bot API URL is always `api.telegram.org`.

**Finding your chat ID:** Send any message to the bot, then check the `getUpdates` response for `message.chat.id`. The setup wizard will automate this.

### Markdown Formatting

Telegram supports MarkdownV2 with rich formatting but requires escaping ~20 special characters. **MVP sends plain text.** MarkdownV2 support is a follow-up.

### Setup Wizard Integration

`leontes init` gains a Telegram setup step:

1. Ask if user wants to enable Telegram support
2. Prompt for bot token (guide user to @BotFather if they don't have one)
3. Store bot token in Worker User Secrets
4. Verify the token works
5. Instruct user to send a message to the bot, then auto-capture their chat ID
6. Store chat ID in Worker User Secrets
7. Send a test message back to confirm everything works

### Error Scenarios

| Scenario | Behavior |
|---|---|
| Bot token not configured | Log "Telegram bridge is disabled", return |
| Bot token invalid (401) | Log error, return (don't retry a bad token) |
| Telegram API unreachable | Log warning, retry with backoff |
| Telegram API returns errors | Log error, continue polling |
| Leontes API unreachable | Log error, send Telegram reply: "I'm having trouble processing your message. Please try again." |
| Message from unknown chat ID | Log warning, ignore |
| Telegram rate limiting (429) | Respect `retry_after` from response, log warning |
| Long-poll timeout (no messages) | Normal — immediately start next poll |

### Security

- Only `AllowedChatIds` can interact with Leontes — all other messages are silently ignored
- Bot token stored in User Secrets, never committed
- Communication with Telegram API is over HTTPS (TLS) — encrypted in transit
- Telegram standard chats are **not** E2E encrypted — for E2E needs, use Signal
- Bot cannot initiate conversations — user must message first (Telegram bot policy)
- Webhook disabled on startup to prevent message leakage to a stale URL

### Testing

Unit tests mirroring the Signal test pattern:
- Telegram client tests: message receiving/parsing, offset tracking, message sending, availability check, error handling (401, 429, network errors)
- Message splitter tests: splitting at 4096 boundary, sentence-boundary splitting, edge cases

## Out of Scope

- Webhook mode (long polling is sufficient for single-user local deployment)
- Telegram Secret Chats (not available for bots)
- Group chat support (1:1 private chats only)
- Inline mode, keyboards, callbacks, or other Telegram bot features
- File/media attachments — text only
- MarkdownV2 formatting (MVP sends plain text)
- Multiple bot accounts
- Generic bridge base class (bridges stay separate — shared contract is the interface)
