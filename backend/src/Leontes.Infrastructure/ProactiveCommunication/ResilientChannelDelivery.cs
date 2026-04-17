using System.Text.Json;
using Leontes.Application;
using Leontes.Application.Configuration;
using Leontes.Application.Messaging;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.ProactiveCommunication;

/// <summary>
/// Delivers outbound messages with per-channel retry, CLI fallback, and an
/// offline queue persisted via <see cref="StoredProactiveEvent"/> (feature 85).
/// </summary>
public sealed class ResilientChannelDelivery(
    IEnumerable<IMessagingClient> clients,
    IApplicationDbContext db,
    IOptions<ResilienceOptions> options,
    ILogger<ResilientChannelDelivery> logger) : IResilientChannelDelivery
{
    private readonly ChannelDeliveryOptions _options = options.Value.ChannelDelivery;

    public async Task<DeliveryResult> DeliverAsync(
        OutboundMessage message,
        CancellationToken cancellationToken)
    {
        var primary = clients.FirstOrDefault(c => c.Channel == message.PreferredChannel);
        if (primary is null)
        {
            logger.LogWarning(
                "No messaging client registered for channel {Channel}; queuing offline",
                message.PreferredChannel);
            await QueueOfflineAsync(message, "no-client", cancellationToken);
            return new DeliveryResult(false, message.PreferredChannel, null, "no-client");
        }

        var lastError = await TryDeliverAsync(primary, message, cancellationToken);
        if (lastError is null)
        {
            return new DeliveryResult(true, message.PreferredChannel, null, null);
        }

        // Fallback to CLI if the preferred channel is not CLI
        if (message.PreferredChannel != MessageChannel.Cli)
        {
            var cli = clients.FirstOrDefault(c => c.Channel == MessageChannel.Cli);
            if (cli is not null)
            {
                var fallbackError = await TryDeliverAsync(cli, message, cancellationToken);
                if (fallbackError is null)
                {
                    logger.LogInformation(
                        "Delivered via CLI fallback after {Channel} failure",
                        message.PreferredChannel);
                    return new DeliveryResult(true, message.PreferredChannel, MessageChannel.Cli, lastError);
                }
                lastError = $"{lastError}; cli-fallback: {fallbackError}";
            }
        }

        await QueueOfflineAsync(message, lastError, cancellationToken);
        return new DeliveryResult(false, message.PreferredChannel, null, lastError);
    }

    private async Task<string?> TryDeliverAsync(
        IMessagingClient client,
        OutboundMessage message,
        CancellationToken cancellationToken)
    {
        var maxAttempts = 1 + Math.Max(0, _options.MaxRetries);
        var baseDelay = TimeSpan.FromSeconds(Math.Max(1, _options.RetryDelaySeconds));

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await client.SendMessageAsync(message.Recipient, message.Content, cancellationToken);
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex,
                    "Delivery via {Channel} attempt {Attempt}/{Max} failed; retrying",
                    client.Channel, attempt, maxAttempts);
                var delay = TimeSpan.FromTicks(baseDelay.Ticks * attempt);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Delivery via {Channel} failed after {Attempts} attempts",
                    client.Channel, attempt);
                return ex.Message;
            }
        }

        return "exhausted-retries";
    }

    private async Task QueueOfflineAsync(
        OutboundMessage message,
        string? reason,
        CancellationToken cancellationToken)
    {
        var stored = new StoredProactiveEvent
        {
            EventType = "OfflineDelivery",
            PayloadJson = JsonSerializer.Serialize(new
            {
                recipient = message.Recipient,
                content = message.Content,
                preferredChannel = message.PreferredChannel.ToString(),
                reason
            }),
            Urgency = ProactiveUrgency.Medium,
            Status = ProactiveEventStatus.Pending,
            RequestId = message.RequestId
        };
        db.Add(stored);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Queued offline message for recipient {Recipient} (reason={Reason})",
            message.Recipient, reason);
    }
}
