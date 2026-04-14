using System.Text.Json;
using Leontes.Application.Messaging;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.Telegram;
using Leontes.Worker.Messaging;
using Microsoft.Extensions.Options;

namespace Leontes.Worker.Telegram;

public sealed class TelegramBridgeService(
    ILogger<TelegramBridgeService> logger,
    IConfiguration configuration,
    IEnumerable<IMessagingClient> messagingClients,
    IHttpClientFactory httpClientFactory,
    TelegramBotClient telegramBotClient,
    IOptions<TelegramOptions> telegramOptions) : BackgroundService
{
    private const int MaxTelegramMessageLength = 4096;

    private readonly IMessagingClient _telegramClient = messagingClients
        .Single(c => c.Channel == MessageChannel.Telegram);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var apiKey = configuration["Authentication:ApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("Authentication:ApiKey is not configured — Telegram bridge will not be able to authenticate with the API");
            return;
        }

        var options = telegramOptions.Value;

        if (string.IsNullOrEmpty(options.BotToken))
        {
            logger.LogWarning("Telegram:BotToken is not configured — Telegram bridge is disabled");
            return;
        }

        if (options.AllowedChatIds.Count == 0)
            logger.LogWarning("Telegram:AllowedChatIds is empty — all incoming messages will be rejected until at least one chat ID is configured");

        if (!await _telegramClient.IsAvailableAsync(stoppingToken))
        {
            logger.LogError("Telegram bot token is invalid or Telegram API is unreachable — Telegram bridge is disabled");
            return;
        }

        logger.LogInformation("Telegram bridge service starting — long-polling with {Timeout}s timeout", options.PollTimeoutSeconds);

        await telegramBotClient.DeleteWebhookAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _telegramClient.ReceiveMessagesAsync(stoppingToken);

                foreach (var message in messages)
                {
                    if (!IsAllowedChat(message.Sender, options.AllowedChatIds))
                    {
                        logger.LogWarning("Ignored Telegram message from unknown chat {ChatId}", message.Sender);
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
                logger.LogError(ex, "Error during Telegram message polling");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("Telegram bridge service stopping");
    }

    private async Task ProcessMessageAsync(IncomingMessage message, string apiKey, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing Telegram message from chat {ChatId}", message.Sender);

        try
        {
            var responseText = await ForwardToApiAsync(message.Content, apiKey, cancellationToken);

            if (string.IsNullOrEmpty(responseText))
            {
                logger.LogWarning("Empty AI response for message from chat {ChatId}", message.Sender);
                return;
            }

            var chunks = MessageSplitter.Split(responseText, MaxTelegramMessageLength);
            foreach (var chunk in chunks)
            {
                await _telegramClient.SendMessageAsync(message.Sender, chunk, cancellationToken);

                if (chunks.Count > 1)
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process Telegram message from chat {ChatId}", message.Sender);

            try
            {
                await _telegramClient.SendMessageAsync(
                    message.Sender,
                    "I'm having trouble processing your message. Please try again.",
                    cancellationToken);
            }
            catch (Exception sendEx)
            {
                logger.LogError(sendEx, "Failed to send error reply to chat {ChatId}", message.Sender);
            }
        }
    }

    private async Task<string> ForwardToApiAsync(string content, string apiKey, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("LeontesApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/messages");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var payload = JsonSerializer.Serialize(new { content, channel = "Telegram" });
        request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await SseResponseReader.ReadSseResponseAsync(response, cancellationToken);
    }

    private static bool IsAllowedChat(string sender, List<long> allowedChatIds)
    {
        if (allowedChatIds.Count == 0)
            return false;

        return long.TryParse(sender, out var chatId) && allowedChatIds.Contains(chatId);
    }
}
