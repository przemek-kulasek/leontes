using System.Net.Http.Headers;

namespace Leontes.Integration.Tests.Infrastructure;

public static class HttpClientExtensions
{
    public static HttpClient WithApiKey(this HttpClient client, string apiKey)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }
}
