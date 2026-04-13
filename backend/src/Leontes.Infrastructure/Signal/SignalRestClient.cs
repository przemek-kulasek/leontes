using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Leontes.Application.Signal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Signal;

public sealed class SignalRestClient(
    HttpClient httpClient,
    IOptions<SignalOptions> options,
    ILogger<SignalRestClient> logger) : ISignalClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<SignalIncomingMessage>> ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        var phoneNumber = options.Value.PhoneNumber;

        var response = await httpClient.GetAsync($"/v1/receive/{phoneNumber}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var envelopes = await response.Content.ReadFromJsonAsync<List<SignalEnvelope>>(JsonOptions, cancellationToken)
            ?? [];

        var messages = new List<SignalIncomingMessage>();

        foreach (var envelope in envelopes)
        {
            var data = envelope.Envelope;
            if (data?.DataMessage?.Message is null)
                continue;

            messages.Add(new SignalIncomingMessage(
                Sender: data.Source ?? string.Empty,
                Content: data.DataMessage.Message,
                Timestamp: data.DataMessage.Timestamp));
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
