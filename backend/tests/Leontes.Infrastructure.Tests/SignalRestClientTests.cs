using System.Net;
using System.Text;
using System.Text.Json;
using Leontes.Infrastructure.Signal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests;

public sealed class SignalRestClientTests
{
    private static readonly SignalOptions DefaultOptions = new()
    {
        BaseUrl = "http://localhost:8081",
        PhoneNumber = "+1234567890",
        PollIntervalSeconds = 2,
        AllowedSenders = ["+0987654321"]
    };

    [Fact]
    public async Task ReceiveMessagesAsync_WithValidEnvelopes_ReturnsParsedMessages()
    {
        var responseJson = """
        [
            {
                "envelope": {
                    "source": "+0987654321",
                    "dataMessage": {
                        "message": "Hello Leontes",
                        "timestamp": 1234567890
                    }
                }
            }
        ]
        """;

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Single(messages);
        Assert.Equal("+0987654321", messages[0].Sender);
        Assert.Equal("Hello Leontes", messages[0].Content);
        Assert.Equal(1234567890, messages[0].Timestamp);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithEmptyArray_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, "[]");

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithNullDataMessage_SkipsEnvelope()
    {
        var responseJson = """
        [
            {
                "envelope": {
                    "source": "+0987654321",
                    "dataMessage": null
                }
            }
        ]
        """;

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithMultipleMessages_ReturnsAll()
    {
        var responseJson = """
        [
            {
                "envelope": {
                    "source": "+111",
                    "dataMessage": { "message": "First", "timestamp": 1 }
                }
            },
            {
                "envelope": {
                    "source": "+222",
                    "dataMessage": { "message": "Second", "timestamp": 2 }
                }
            }
        ]
        """;

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Equal(2, messages.Count);
        Assert.Equal("First", messages[0].Content);
        Assert.Equal("Second", messages[1].Content);
    }

    [Fact]
    public async Task SendMessageAsync_SendsCorrectPayload()
    {
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}", request =>
        {
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var client = CreateClient(handler);

        await client.SendMessageAsync("+0987654321", "Hello from Leontes", CancellationToken.None);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        Assert.Equal("Hello from Leontes", doc.RootElement.GetProperty("message").GetString());
        Assert.Equal("+1234567890", doc.RootElement.GetProperty("number").GetString());
        Assert.Equal("+0987654321", doc.RootElement.GetProperty("recipients")[0].GetString());
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiResponds_ReturnsTrue()
    {
        var client = CreateClient(HttpStatusCode.OK, "{}");

        var result = await client.IsAvailableAsync(CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiDown_ReturnsFalse()
    {
        var handler = new FakeHttpMessageHandler(new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        var result = await client.IsAvailableAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_ServerError_ThrowsHttpRequestException()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, "error");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ReceiveMessagesAsync(CancellationToken.None));
    }

    private static SignalRestClient CreateClient(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        return CreateClient(handler);
    }

    private static SignalRestClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8081") };
        var options = Options.Create(DefaultOptions);
        return new SignalRestClient(httpClient, options, NullLogger<SignalRestClient>.Instance);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        private readonly Action<HttpRequestMessage>? _onRequest;
        private readonly Exception? _exception;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody, Action<HttpRequestMessage>? onRequest = null)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _onRequest = onRequest;
        }

        public FakeHttpMessageHandler(Exception exception)
        {
            _exception = exception;
            _statusCode = HttpStatusCode.OK;
            _responseBody = string.Empty;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_exception is not null)
                throw _exception;

            _onRequest?.Invoke(request);

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
