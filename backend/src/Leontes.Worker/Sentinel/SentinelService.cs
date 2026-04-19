using System.Text;
using System.Text.Json;
using Leontes.Application.Sentinel;
using Leontes.Worker.Messaging;
using Microsoft.Extensions.Options;

namespace Leontes.Worker.Sentinel;

public sealed class SentinelService(
    ILogger<SentinelService> logger,
    IConfiguration configuration,
    IOptions<SentinelOptions> options,
    ISentinelEventQueue queue,
    ISentinelRateLimiter rateLimiter,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private readonly SentinelOptions _options = options.Value;

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

            await SseResponseReader.ReadSseResponseAsync(response, cancellationToken);

            logger.LogInformation(
                "Sentinel event escalated: {MonitorSource}/{Pattern} — {Summary}",
                evt.MonitorSource, evt.Pattern, evt.Summary);
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

    private static string BuildPayload(SentinelEvent evt)
    {
        var content = FormatContent(evt);
        return JsonSerializer.Serialize(new
        {
            content,
            channel = "Sentinel",
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
