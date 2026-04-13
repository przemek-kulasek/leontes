using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Leontes.Integration.Tests.Infrastructure;

namespace Leontes.Integration.Tests;

public sealed class ChatEndpointTests(LeontesApiFactory factory)
    : IClassFixture<LeontesApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient()
        .WithApiKey(LeontesApiFactory.TestApiKey);

    [Fact]
    public async Task SendMessage_ValidRequest_StreamsResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonSerializer.Serialize(new { content = "Hello", channel = "cli" });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/messages")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("event: chunk", body);
        Assert.Contains("event: done", body);
    }

    [Fact]
    public async Task GetMessages_WithoutConversationId_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/v1/messages", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMessages_WithNonExistentConversation_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var conversationId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/v1/messages?conversationId={conversationId}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var messages = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(0, messages.GetArrayLength());
    }
}
