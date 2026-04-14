using System.Net;
using System.Text;
using System.Text.Json;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.Telegram;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.Tests;

public sealed class TelegramBotClientTests
{
    private static readonly TelegramOptions DefaultOptions = new()
    {
        BotToken = "123456:ABC-DEF",
        PollTimeoutSeconds = 30,
        AllowedChatIds = [67890]
    };

    [Fact]
    public async Task ReceiveMessagesAsync_WithValidUpdate_ReturnsParsedMessage()
    {
        var responseJson = """
        {
            "ok": true,
            "result": [
                {
                    "update_id": 100,
                    "message": {
                        "message_id": 1,
                        "chat": { "id": 67890, "type": "private" },
                        "date": 1234567890,
                        "text": "Hello Leontes"
                    }
                }
            ]
        }
        """;

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Single(messages);
        Assert.Equal("67890", messages[0].Sender);
        Assert.Equal("Hello Leontes", messages[0].Content);
        Assert.Equal(1234567890, messages[0].Timestamp);
        Assert.Equal(MessageChannel.Telegram, messages[0].Channel);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithEmptyResult_ReturnsEmptyList()
    {
        var responseJson = """{"ok": true, "result": []}""";

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithNullText_SkipsUpdate()
    {
        var responseJson = """
        {
            "ok": true,
            "result": [
                {
                    "update_id": 100,
                    "message": {
                        "message_id": 1,
                        "chat": { "id": 67890, "type": "private" },
                        "date": 1234567890,
                        "text": null
                    }
                }
            ]
        }
        """;

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithMultipleUpdates_ReturnsAll()
    {
        var responseJson = """
        {
            "ok": true,
            "result": [
                {
                    "update_id": 100,
                    "message": {
                        "message_id": 1,
                        "chat": { "id": 67890, "type": "private" },
                        "date": 1,
                        "text": "First"
                    }
                },
                {
                    "update_id": 101,
                    "message": {
                        "message_id": 2,
                        "chat": { "id": 67890, "type": "private" },
                        "date": 2,
                        "text": "Second"
                    }
                }
            ]
        }
        """;

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Equal(2, messages.Count);
        Assert.Equal("First", messages[0].Content);
        Assert.Equal("Second", messages[1].Content);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_TracksOffset()
    {
        var responseJson = """
        {
            "ok": true,
            "result": [
                {
                    "update_id": 42,
                    "message": {
                        "message_id": 1,
                        "chat": { "id": 67890, "type": "private" },
                        "date": 1,
                        "text": "Hello"
                    }
                }
            ]
        }
        """;

        string? capturedUrl = null;
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseJson, request =>
        {
            callCount++;
            if (callCount == 2)
                capturedUrl = request.RequestUri?.ToString();
            return Task.CompletedTask;
        });

        var client = CreateClient(handler);

        await client.ReceiveMessagesAsync(CancellationToken.None);
        await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.NotNull(capturedUrl);
        Assert.Contains("offset=43", capturedUrl);
    }

    [Fact]
    public async Task SendMessageAsync_SendsCorrectPayload()
    {
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"ok":true,"result":{}}""", async request =>
        {
            capturedBody = request.Content is not null
                ? await request.Content.ReadAsStringAsync()
                : null;
        });

        var client = CreateClient(handler);

        await client.SendMessageAsync("67890", "Hello from Leontes", CancellationToken.None);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        Assert.Equal("Hello from Leontes", doc.RootElement.GetProperty("text").GetString());
        Assert.Equal(67890, doc.RootElement.GetProperty("chat_id").GetInt64());
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiResponds_ReturnsTrue()
    {
        var client = CreateClient(HttpStatusCode.OK, """{"ok":true,"result":{"id":123,"is_bot":true}}""");

        var result = await client.IsAvailableAsync(CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenUnauthorized_ReturnsFalse()
    {
        var client = CreateClient(HttpStatusCode.Unauthorized, """{"ok":false,"description":"Unauthorized"}""");

        var result = await client.IsAvailableAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenNetworkError_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(new HttpRequestException("Connection refused"));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task IsAvailableAsync_WhenServerError_ThrowsHttpRequestException()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, """{"ok":false}""");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReceiveMessagesAsync_ServerError_ThrowsHttpRequestException()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, """{"ok":false,"description":"Internal error"}""");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ReceiveMessagesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithNoMessageField_SkipsUpdate()
    {
        var responseJson = """
        {
            "ok": true,
            "result": [
                {
                    "update_id": 100
                }
            ]
        }
        """;

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithGroupChat_SkipsUpdate()
    {
        var responseJson = """
        {
            "ok": true,
            "result": [
                {
                    "update_id": 100,
                    "message": {
                        "message_id": 1,
                        "chat": { "id": 67890, "type": "group" },
                        "date": 1234567890,
                        "text": "Hello from group"
                    }
                }
            ]
        }
        """;

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_WithNullChat_SkipsUpdate()
    {
        var responseJson = """
        {
            "ok": true,
            "result": [
                {
                    "update_id": 100,
                    "message": {
                        "message_id": 1,
                        "date": 1234567890,
                        "text": "No chat object"
                    }
                }
            ]
        }
        """;

        var client = CreateClient(HttpStatusCode.OK, responseJson);

        var messages = await client.ReceiveMessagesAsync(CancellationToken.None);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task ReceiveMessagesAsync_RateLimited_ThrowsTelegramRateLimitedException()
    {
        var responseJson = """{"ok":false,"description":"Too Many Requests","parameters":{"retry_after":30}}""";

        var client = CreateClient(HttpStatusCode.TooManyRequests, responseJson);

        var ex = await Assert.ThrowsAsync<TelegramRateLimitedException>(() =>
            client.ReceiveMessagesAsync(CancellationToken.None));

        Assert.Equal(30, ex.RetryAfterSeconds);
    }

    private static TelegramBotClient CreateClient(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        return CreateClient(handler);
    }

    private static TelegramBotClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.telegram.org") };
        var options = Options.Create(DefaultOptions);
        return new TelegramBotClient(httpClient, options, NullLogger<TelegramBotClient>.Instance);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        private readonly Func<HttpRequestMessage, Task>? _onRequest;
        private readonly Exception? _exception;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody, Func<HttpRequestMessage, Task>? onRequest = null)
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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_exception is not null)
                throw _exception;

            if (_onRequest is not null)
                await _onRequest(request);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
