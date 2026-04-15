# Signal Setup

Signal lets you message Leontes from your phone via E2E encrypted messaging. It uses [signal-cli-rest-api](https://github.com/bbernhard/signal-cli-rest-api) running in Docker. No Java needed on your machine.

**Signal is entirely optional.** The Worker runs Sentinel regardless. If you skip this guide, the bridge logs "Signal bridge is disabled" and does nothing else.

## Disabling Signal

If you previously set up Signal and want to turn it off:

```bash
# 1. Remove the phone number from Worker secrets (this disables the bridge)
dotnet user-secrets remove "Signal:PhoneNumber" --project backend/src/Leontes.Worker

# 2. Stop the signal-cli container (saves resources)
docker compose stop signal
```

The Worker will still start normally. Sentinel keeps running, only the Signal bridge is skipped. To re-enable later, set `Signal:PhoneNumber` again and start the container.

If you never configured Signal at all, there's nothing to disable. It's off by default.

## 1. Start the Signal container

```bash
docker compose up -d signal
```

This starts `signal-cli-rest-api` on port 8081.

## 2. Register a phone number

You need a dedicated phone number (prepaid SIM or VoIP) that can receive SMS. This number becomes Leontes' Signal identity, separate from your personal number.

```bash
# Request SMS verification
curl -X POST http://localhost:8081/v1/register/+YOUR_PHONE_NUMBER

# If a CAPTCHA is required (likely):
# 1. Open https://signalcaptchas.org/registration/generate in your browser
# 2. Solve the CAPTCHA
# 3. Your browser will try to open a signalcaptcha:// link. Copy it from the address bar.
# 4. Strip the "signalcaptcha://" prefix and pass the rest as the captcha value
curl -X POST http://localhost:8081/v1/register/+YOUR_PHONE_NUMBER \
  -H "Content-Type: application/json" \
  -d '{"captcha": "signal-hcaptcha.YOUR_TOKEN_HERE"}'
```

> **CAPTCHA tokens expire in about 2 minutes.** Solve the CAPTCHA and run the curl command immediately.

## 3. Verify registration

After receiving the SMS code:

```bash
curl -X POST http://localhost:8081/v1/register/+YOUR_PHONE_NUMBER/verify/CODE
```

## 4. Configure Leontes Worker

```bash
# Set the registered phone number (this enables the Signal bridge)
dotnet user-secrets set "Signal:PhoneNumber" "+YOUR_PHONE_NUMBER" \
  --project backend/src/Leontes.Worker

# Allow your personal phone to send messages to Leontes
dotnet user-secrets set "Signal:AllowedSenders:0" "+YOUR_PERSONAL_NUMBER" \
  --project backend/src/Leontes.Worker
```

## 5. Start the Worker

```bash
dotnet run --project backend/src/Leontes.Worker --configuration Release
```

The Worker will connect to signal-cli-rest-api and start polling for messages. Send a message from your phone to the registered number and Leontes will respond.

## Configuration reference

All Signal settings are stored in Worker user secrets. Nothing goes in `appsettings.json`.

| Setting | Required | Default | Description |
|---------|----------|---------|-------------|
| `Signal:PhoneNumber` | Yes (to enable) | *(none)* | The registered phone number. If empty or missing, the bridge is disabled. |
| `Signal:AllowedSenders:0` | No | (allow all) | Phone numbers allowed to message Leontes. If empty, all senders are accepted. Add more with `:1`, `:2`, etc. |
| `Signal:BaseUrl` | No | `http://localhost:8081` | URL of the signal-cli-rest-api container. Only change if you moved the port. |
| `Signal:PollIntervalSeconds` | No | `2` | How often the bridge checks for new messages. |
| `Authentication:ApiKey` | Yes (to enable) | *(none)* | API key for forwarding messages to the backend. Set by `leontes init`. |

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Worker logs "Signal bridge is disabled" | Set `Signal:PhoneNumber` in Worker user secrets (step 4) |
| Worker logs "Signal REST API not available" | Check `docker compose ps signal`. Container must be running on port 8081. |
| Registration fails with 403 | CAPTCHA required. See step 2 above. |
| Messages ignored silently | Sender not in `AllowedSenders`. Check Worker logs for "unknown sender" warning. |
| Worker logs "ApiKey not configured" | Run `leontes init` to generate and set the API key |
| Port 8081 already in use | Change the port mapping in `docker-compose.yml` and update `Signal:BaseUrl` in Worker user secrets |
