using System.Text.Json;
using Leontes.Application.Signal;
using Leontes.Infrastructure.Signal;
using Microsoft.Extensions.Options;

namespace Leontes.Worker.Signal;

public sealed class SignalBridgeService(
    ILogger<SignalBridgeService> logger,
    IConfiguration configuration,
    ISignalClient signalClient,
    IHttpClientFactory httpClientFactory,
    IOptions<SignalOptions> signalOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var apiKey = configuration["Authentication:ApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("Authentication:ApiKey is not configured — Signal bridge will not be able to authenticate with the API");
            return;
        }

        var options = signalOptions.Value;

        if (string.IsNullOrEmpty(options.PhoneNumber))
        {
            logger.LogWarning("Signal:PhoneNumber is not configured — Signal bridge is disabled");
            return;
        }

        if (options.AllowedSenders.Count == 0)
            logger.LogWarning("Signal:AllowedSenders is empty — all incoming messages will be rejected until at least one sender is configured");

        logger.LogInformation("Signal bridge service starting — polling every {PollInterval}s", options.PollIntervalSeconds);

        await WaitForSignalAvailabilityAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await signalClient.ReceiveMessagesAsync(stoppingToken);

                foreach (var message in messages)
                {
                    if (!IsAllowedSender(message.Sender, options.AllowedSenders))
                    {
                        logger.LogWarning("Ignored Signal message from unknown sender {Sender}", message.Sender);
                        continue;
                    }

                    await ProcessMessageAsync(message, apiKey, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Signal message polling");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
        }

        logger.LogInformation("Signal bridge service stopping");
    }

    private async Task WaitForSignalAvailabilityAsync(CancellationToken cancellationToken)
    {
        var delaySeconds = 5;
        const int maxDelaySeconds = 60;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (await signalClient.IsAvailableAsync(cancellationToken))
            {
                logger.LogInformation("Signal REST API is available");
                return;
            }

            logger.LogWarning("Signal REST API not available, retrying in {Delay}s", delaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            delaySeconds = Math.Min(delaySeconds * 2, maxDelaySeconds);
        }
    }

    private async Task ProcessMessageAsync(SignalIncomingMessage message, string apiKey, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing Signal message from {Sender}", message.Sender);

        try
        {
            var responseText = await ForwardToApiAsync(message.Content, apiKey, cancellationToken);

            if (string.IsNullOrEmpty(responseText))
            {
                logger.LogWarning("Empty AI response for message from {Sender}", message.Sender);
                return;
            }

            var chunks = SignalMessageHelper.SplitMessage(responseText);
            foreach (var chunk in chunks)
            {
                await signalClient.SendMessageAsync(message.Sender, chunk, cancellationToken);

                if (chunks.Count > 1)
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process Signal message from {Sender}", message.Sender);

            try
            {
                await signalClient.SendMessageAsync(
                    message.Sender,
                    "I'm having trouble processing your message. Please try again.",
                    cancellationToken);
            }
            catch (Exception sendEx)
            {
                logger.LogError(sendEx, "Failed to send error reply to {Sender}", message.Sender);
            }
        }
    }

    private async Task<string> ForwardToApiAsync(string content, string apiKey, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("LeontesApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/messages");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var payload = JsonSerializer.Serialize(new { content, channel = "Signal" });
        request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await SignalMessageHelper.ReadSseResponseAsync(response, cancellationToken);
    }

    private static bool IsAllowedSender(string sender, List<string> allowedSenders)
    {
        if (allowedSenders.Count == 0)
            return false;

        return allowedSenders.Contains(sender, StringComparer.OrdinalIgnoreCase);
    }
}
