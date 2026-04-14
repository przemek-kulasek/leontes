using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Leontes.Application.Messaging;
using Leontes.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Signal;

public sealed class SignalRestClient(
    HttpClient httpClient,
    IOptions<SignalOptions> options,
    ILogger<SignalRestClient> logger) : IMessagingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MessageChannel Channel => MessageChannel.Signal;

    public async Task<IReadOnlyList<IncomingMessage>> ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        var phoneNumber = options.Value.PhoneNumber;

        var response = await httpClient.GetAsync($"/v1/receive/{phoneNumber}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Signal receive failed with {StatusCode}: {Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var envelopes = await response.Content.ReadFromJsonAsync<List<SignalEnvelope>>(JsonOptions, cancellationToken)
            ?? [];

        var messages = new List<IncomingMessage>();

        foreach (var envelope in envelopes)
        {
            var data = envelope.Envelope;
            if (data?.DataMessage?.Message is null)
                continue;

            if (string.IsNullOrEmpty(data.Source))
                continue;

            messages.Add(new IncomingMessage(
                Sender: data.Source,
                Content: data.DataMessage.Message,
                Timestamp: data.DataMessage.Timestamp,
                Channel: MessageChannel.Signal));
        }

        if (messages.Count > 0)
            logger.LogInformation("Received {MessageCount} Signal message(s) from {PhoneNumber}", messages.Count, phoneNumber);

        return messages;
    }

    public async Task SendMessageAsync(string recipient, string message, CancellationToken cancellationToken)
    {
        var phoneNumber = options.Value.PhoneNumber;

        var payload = new SignalSendRequest
        {
            Message = message,
            Number = phoneNumber,
            Recipients = [recipient]
        };

        var response = await httpClient.PostAsJsonAsync("/v2/send", payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("Sent Signal message to {Recipient} ({Length} chars)", recipient, message.Length);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync("/v1/about", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Signal REST API availability check failed");
            return false;
        }
    }

    private sealed class SignalEnvelope
    {
        [JsonPropertyName("envelope")]
        public SignalEnvelopeData? Envelope { get; set; }
    }

    private sealed class SignalEnvelopeData
    {
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("dataMessage")]
        public SignalDataMessage? DataMessage { get; set; }
    }

    private sealed class SignalDataMessage
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }

    private sealed class SignalSendRequest
    {
        [JsonPropertyName("message")]
        public required string Message { get; set; }

        [JsonPropertyName("number")]
        public required string Number { get; set; }

        [JsonPropertyName("recipients")]
        public required List<string> Recipients { get; set; }
    }
}
