using System.Text;
using System.Text.Json;
using Leontes.Application.Messaging;
using Leontes.Application.Sentinel;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.Telegram;
using Leontes.Worker.Messaging;
using Microsoft.Extensions.Options;

namespace Leontes.Worker.Sentinel;

public sealed class SentinelService(
    ILogger<SentinelService> logger,
    IConfiguration configuration,
    IOptions<SentinelOptions> options,
    IOptions<TelegramOptions> telegramOptions,
    ISentinelEventQueue queue,
    ISentinelRateLimiter rateLimiter,
    IHttpClientFactory httpClientFactory,
    IEnumerable<IMessagingClient> messagingClients) : BackgroundService
{
    private const int MaxTelegramMessageLength = 4096;

    private readonly SentinelOptions _options = options.Value;
    private readonly TelegramOptions _telegramOptions = telegramOptions.Value;
    private readonly IMessagingClient? _telegramClient = messagingClients
        .FirstOrDefault(c => c.Channel == MessageChannel.Telegram);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Sentinel service disabled via configuration");
            return;
        }

        var apiKey = configuration["Authentication:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("Authentication:ApiKey is not configured — Sentinel escalations will not be forwarded to the API");
            return;
        }

        logger.LogInformation("Sentinel service starting — rate limit {Limit}/min per monitor", _options.RateLimitPerMonitorPerMinute);

        try
        {
            await foreach (var sentinelEvent in queue.ReadAllAsync(stoppingToken))
            {
                if (!rateLimiter.TryAcquire(sentinelEvent.MonitorSource, DateTime.UtcNow))
                {
                    logger.LogDebug(
                        "Rate limit hit for {MonitorSource}; dropping event {Pattern}",
                        sentinelEvent.MonitorSource, sentinelEvent.Pattern);
                    continue;
                }

                await ForwardAsync(sentinelEvent, apiKey, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        logger.LogInformation("Sentinel service stopping");
    }

    private async Task ForwardAsync(SentinelEvent evt, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("LeontesApi");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/messages");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(BuildPayload(evt), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseText = await SseResponseReader.ReadSseResponseAsync(response, cancellationToken);

            logger.LogInformation(
                "Sentinel event escalated: {MonitorSource}/{Pattern} — {Summary}",
                evt.MonitorSource, evt.Pattern, evt.Summary);

            await PushToTelegramAsync(evt, responseText, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Sentinel forwarding failed for {MonitorSource}/{Pattern}",
                evt.MonitorSource, evt.Pattern);
        }
    }

    private async Task PushToTelegramAsync(SentinelEvent evt, string responseText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            logger.LogDebug("Sentinel pipeline produced empty response for {Pattern}; nothing to push", evt.Pattern);
            return;
        }

        if (_telegramClient is null)
        {
            logger.LogDebug("No Telegram client registered; skipping proactive push for {Pattern}", evt.Pattern);
            return;
        }

        if (_telegramOptions.AllowedChatIds.Count == 0)
        {
            logger.LogWarning("Telegram:AllowedChatIds is empty; cannot deliver proactive Sentinel notification");
            return;
        }

        var header = $"🔔 Sentinel · {evt.MonitorSource}/{evt.Pattern} ({evt.Priority})\n";
        var fullText = header + responseText;
        var chunks = MessageSplitter.Split(fullText, MaxTelegramMessageLength);

        foreach (var chatId in _telegramOptions.AllowedChatIds)
        {
            var recipient = chatId.ToString();
            try
            {
                foreach (var chunk in chunks)
                {
                    await _telegramClient.SendMessageAsync(recipient, chunk, cancellationToken);
                    if (chunks.Count > 1)
                        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }

                logger.LogInformation(
                    "Sentinel notification pushed to Telegram chat {ChatId} for {Pattern}",
                    chatId, evt.Pattern);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to push Sentinel notification to Telegram chat {ChatId}",
                    chatId);
            }
        }
    }

    private static string BuildPayload(SentinelEvent evt)
    {
        var content = FormatContent(evt);
        return JsonSerializer.Serialize(new
        {
            content,
            channel = "Sentinel",
            conversationId = Guid.NewGuid(),
            metadata = new
            {
                monitorSource = evt.MonitorSource,
                eventType = evt.EventType,
                pattern = evt.Pattern,
                priority = evt.Priority.ToString(),
                occurredAt = evt.OccurredAt,
                details = evt.Metadata
            }
        });
    }

    private static string FormatContent(SentinelEvent evt)
    {
        var builder = new StringBuilder();
        builder.Append("[Sentinel: ").Append(evt.MonitorSource).Append("] ").AppendLine(evt.Summary);
        if (!string.IsNullOrEmpty(evt.Pattern))
            builder.Append("Pattern: ").AppendLine(evt.Pattern);
        builder.Append("Priority: ").Append(evt.Priority);
        return builder.ToString();
    }
}
