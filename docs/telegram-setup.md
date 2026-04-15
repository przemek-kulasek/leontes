# Telegram Setup

Telegram lets you message Leontes from your phone via the official [Telegram Bot API](https://core.telegram.org/bots/api). No SIM card, no Docker container. Just an HTTPS bot token.

**Telegram is entirely optional.** If you skip this guide, the bridge logs "Telegram bridge is disabled" and does nothing else.

## Disabling Telegram

If you previously set up Telegram and want to turn it off:

```bash
# Remove the bot token from Worker secrets (this disables the bridge)
dotnet user-secrets remove "Telegram:BotToken" --project backend/src/Leontes.Worker
```

The Worker will still start normally. Sentinel and Signal keep running, only the Telegram bridge is skipped.

## 1. Create a Telegram bot

1. Open Telegram and search for **@BotFather**
2. Send `/newbot` and follow the prompts (choose a name and username)
3. BotFather will reply with a **bot token**. Copy it.

## 2. Configure Leontes Worker

```bash
# Set the bot token (this enables the Telegram bridge)
dotnet user-secrets set "Telegram:BotToken" "YOUR_BOT_TOKEN" \
  --project backend/src/Leontes.Worker
```

## 3. Find your Telegram chat ID

Send any message to your bot in Telegram, then run:

```bash
curl https://api.telegram.org/botYOUR_BOT_TOKEN/getUpdates
```

Look for `"chat": { "id": 12345678 }` in the response. That number is your chat ID.

```bash
# Allow your Telegram account to message Leontes
dotnet user-secrets set "Telegram:AllowedChatIds:0" "YOUR_CHAT_ID" \
  --project backend/src/Leontes.Worker
```

## 4. Start the Worker

```bash
dotnet run --project backend/src/Leontes.Worker --configuration Release
```

The Worker will connect to the Telegram Bot API and start long-polling for messages. Send a message to your bot and Leontes will respond.

## Configuration reference

All Telegram secrets are stored in Worker user secrets. Non-secret defaults, such as `Telegram:PollTimeoutSeconds`, may live in `appsettings.json`.

| Setting | Required | Default | Description |
|---------|----------|---------|-------------|
| `Telegram:BotToken` | Yes (to enable) | *(none)* | The bot token from @BotFather. If empty or missing, the bridge is disabled. |
| `Telegram:AllowedChatIds:0` | No | (allow none) | Telegram chat IDs allowed to message Leontes. If empty, all messages are rejected. Add more with `:1`, `:2`, etc. |
| `Telegram:PollTimeoutSeconds` | No | `30` | Long-poll timeout in seconds. 30 is Telegram's recommended maximum. |
| `Authentication:ApiKey` | Yes (to enable) | *(none)* | API key for forwarding messages to the backend. Set by `leontes init`. |

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Worker logs "Telegram bridge is disabled" | Set `Telegram:BotToken` in Worker user secrets (step 2) |
| Worker logs "Telegram bot token is invalid" | Verify the token with `curl https://api.telegram.org/botYOUR_TOKEN/getMe` |
| Messages ignored silently | Chat ID not in `AllowedChatIds`. Check Worker logs for "unknown chat" warning. |
| Worker logs "ApiKey not configured" | Run `leontes init` to generate and set the API key |
