using Leontes.Application.Configuration;
using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Application.ThinkingPipeline;
using Leontes.Domain.Enums;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI;

/// <summary>
/// Polls the LLM provider during degraded mode to detect recovery. On a
/// transition either way publishes a <see cref="NotificationEvent"/> so the
/// user is informed (feature 85).
/// </summary>
public sealed class DegradedModeMonitor(
    ILlmAvailability availability,
    [FromKeyedServices("Small")] IChatClient probeClient,
    IWorkflowEventBridge eventBridge,
    IOptions<ResilienceOptions> options,
    ILogger<DegradedModeMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Max(5, options.Value.DegradedMode.LlmPollIntervalSeconds));

        bool previousAvailable = availability.IsAvailable;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!availability.IsAvailable)
            {
                await ProbeAsync(stoppingToken);
            }

            var current = availability.IsAvailable;
            if (current != previousAvailable)
            {
                await NotifyTransitionAsync(current, stoppingToken);
                previousAvailable = current;
            }
        }
    }

    private async Task ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var probe = new List<ChatMessage> { new(ChatRole.User, "ping") };
            await probeClient.GetResponseAsync(probe, cancellationToken: cts.Token);

            availability.RecordSuccess();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "LLM probe failed during degraded mode");
        }
    }

    private async Task NotifyTransitionAsync(bool nowAvailable, CancellationToken cancellationToken)
    {
        var message = nowAvailable
            ? "AI provider recovered — resuming normal operation."
            : "AI provider unreachable — switching to degraded mode (Sentinel and memory still work).";

        var urgency = nowAvailable ? ProactiveUrgency.Low : ProactiveUrgency.High;

        await eventBridge.PublishEventAsync(
            new NotificationEvent("Degraded Mode", message, urgency),
            cancellationToken);

        logger.LogInformation("Degraded mode transition: available={Available}", nowAvailable);
    }
}
