---
paths: "**/*.cs"
---

## Logging Conventions (Serilog)

### Log Levels

| Level | When to use |
|---|---|
| Debug | Detailed diagnostic info useful during development — query parameters, intermediate values, branch decisions |
| Information | Key business events — user registered, order placed, payment processed, migration applied |
| Warning | Something unexpected that the system handled gracefully — missing optional config, deprecated endpoint called, retry triggered |
| Error | An operation failed and the request could not be completed — unhandled exception, external service down, database timeout |
| Fatal | Application cannot continue — startup failure, missing required config, database unreachable on boot |

### Rules
- Use structured logging with message templates — never string interpolation or concatenation:
  ```csharp
  // Good
  _logger.LogInformation("User {UserId} placed order {OrderId}", userId, orderId);

  // Bad
  _logger.LogInformation($"User {userId} placed order {orderId}");
  ```
- Property names in templates use PascalCase: `{UserId}`, `{OrderId}`, `{ElapsedMs}`
- Always include enough context to diagnose the issue without reading the code — who, what, which resource
- Do not log sensitive data: passwords, tokens, full credit card numbers, personal health information
- Do not log the full request/response body in production — log a summary or identifier instead

### Standard Enrichers
Every log entry automatically includes (configured via Serilog enrichers):
- `RequestId` — correlates all logs for a single HTTP request
- `ClientIp` — from `Serilog.Enrichers.ClientInfo`
- `MachineName` — identifies which instance produced the log
- `Timestamp` — UTC

### Where to Log
- **Endpoints:** Log at Information level when a significant action completes (resource created, deleted)
- **Services:** Log at Information for business events, Warning for handled anomalies, Error for failures
- **Infrastructure:** Log at Debug for external API call details, Warning for retries, Error for failures
- **Global exception handler:** Logs all unhandled exceptions at Error level automatically — do not duplicate this logging in individual endpoints

### Correlation
- ASP.NET Core assigns a `TraceIdentifier` to every request — Serilog picks this up as `RequestId`
- When calling external APIs, forward the correlation ID via a custom header (`X-Correlation-Id`) so logs can be traced across services
- Frontend should send a `X-Correlation-Id` header with each API call (generated per user action) to enable end-to-end tracing
