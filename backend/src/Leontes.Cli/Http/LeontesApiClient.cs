using System.Net.Http.Headers;

namespace Leontes.Cli.Http;

public sealed class LeontesApiClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public LeontesApiClient(string? baseUrl = null, string? apiKey = null)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl ?? "http://localhost:5000")
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/_health", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
