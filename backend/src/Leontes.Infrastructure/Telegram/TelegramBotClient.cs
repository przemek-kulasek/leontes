using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Leontes.Application.Messaging;
using Leontes.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Telegram;

public sealed class TelegramBotClient(
    HttpClient httpClient,
    IOptions<TelegramOptions> options,
    ILogger<TelegramBotClient> logger) : IMessagingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private long _offset;

    public MessageChannel Channel => MessageChannel.Telegram;

    public async Task<IReadOnlyList<IncomingMessage>> ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        var token = options.Value.BotToken;
        var timeout = options.Value.PollTimeoutSeconds;

        var url = $"/bot{token}/getUpdates?timeout={timeout}&offset={_offset}";

        var response = await httpClient.GetAsync(url, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "getUpdates", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<TelegramResponse<List<TelegramUpdate>>>(JsonOptions, cancellationToken);

        if (result?.Result is null || result.Result.Count == 0)
            return [];

        var messages = new List<IncomingMessage>();

        foreach (var update in result.Result)
        {
            _offset = update.UpdateId + 1;

            if (update.Message?.Text is null || update.Message.Chat is null)
                continue;

            if (update.Message.Chat.Type != "private")
            {
                logger.LogDebug("Skipped non-private Telegram chat {ChatId} (type: {ChatType})",
                    update.Message.Chat.Id, update.Message.Chat.Type);
                continue;
            }

            var chatId = update.Message.Chat.Id.ToString();

            messages.Add(new IncomingMessage(
                Sender: chatId,
                Content: update.Message.Text,
                Timestamp: update.Message.Date,
                Channel: MessageChannel.Telegram));
        }

        if (messages.Count > 0)
            logger.LogInformation("Received {MessageCount} Telegram message(s)", messages.Count);

        return messages;
    }

    public async Task SendMessageAsync(string recipient, string message, CancellationToken cancellationToken)
    {
        var token = options.Value.BotToken;

        var payload = new TelegramSendRequest
        {
            ChatId = long.Parse(recipient),
            Text = message
        };

        var response = await httpClient.PostAsJsonAsync($"/bot{token}/sendMessage", payload, JsonOptions, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "sendMessage", cancellationToken);

        logger.LogInformation("Sent Telegram message to chat {ChatId} ({Length} chars)", recipient, message.Length);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        var token = options.Value.BotToken;
        var response = await httpClient.GetAsync($"/bot{token}/getMe", cancellationToken);

        if (response.IsSuccessStatusCode)
            return true;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogError("Telegram bot token is invalid (401 Unauthorized)");
            return false;
        }

        logger.LogWarning("Telegram API returned {StatusCode} during availability check", (int)response.StatusCode);
        throw new HttpRequestException($"Telegram API returned {(int)response.StatusCode} during availability check");
    }

    public async Task DeleteWebhookAsync(CancellationToken cancellationToken)
    {
        var token = options.Value.BotToken;
        var response = await httpClient.PostAsync($"/bot{token}/deleteWebhook", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "deleteWebhook", cancellationToken);
        logger.LogDebug("Telegram webhook deleted to ensure polling mode");
    }

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string method, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = 5;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("parameters", out var parameters) &&
                    parameters.TryGetProperty("retry_after", out var retryElement))
                {
                    retryAfter = retryElement.GetInt32();
                }
            }
            catch (JsonException)
            {
                // Ignore parse failure, use default
            }

            logger.LogWarning("Telegram rate limited on {Method}, retry after {RetryAfter}s", method, retryAfter);
            throw new TelegramRateLimitedException(retryAfter, method);
        }

        logger.LogError("Telegram {Method} failed with {StatusCode}: {Body}", method, (int)response.StatusCode, body);
        response.EnsureSuccessStatusCode();
    }

    private sealed class TelegramResponse<T>
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }
    }

    private sealed class TelegramUpdate
    {
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessage? Message { get; set; }
    }

    private sealed class TelegramMessage
    {
        [JsonPropertyName("message_id")]
        public long MessageId { get; set; }

        [JsonPropertyName("chat")]
        public TelegramChat? Chat { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class TelegramChat
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    private sealed class TelegramSendRequest
    {
        [JsonPropertyName("chat_id")]
        public long ChatId { get; set; }

        [JsonPropertyName("text")]
        public required string Text { get; set; }
    }
}
