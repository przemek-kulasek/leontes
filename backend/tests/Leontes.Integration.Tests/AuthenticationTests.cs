using System.Net;
using System.Net.Http.Headers;
using Leontes.Integration.Tests.Infrastructure;

namespace Leontes.Integration.Tests;

public sealed class AuthenticationTests(LeontesApiFactory factory)
    : IClassFixture<LeontesApiFactory>
{
    [Fact]
    public async Task ApiEndpoint_WithoutApiKey_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/messages?conversationId=" + Guid.NewGuid(), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoint_WithInvalidApiKey_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "lnt_wrong-key");

        var response = await client.GetAsync("/api/v1/messages?conversationId=" + Guid.NewGuid(), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiEndpoint_WithValidApiKey_ReturnsSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient()
            .WithApiKey(LeontesApiFactory.TestApiKey);

        var response = await client.GetAsync("/api/v1/messages?conversationId=" + Guid.NewGuid(), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_WithoutApiKey_ReturnsSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_health", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
