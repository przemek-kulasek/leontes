using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Leontes.Cli.Http;

public sealed class LeontesApiClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public LeontesApiClient(string? baseUrl = null, string? apiKey = null)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl ?? "http://localhost:5154"),
            Timeout = TimeSpan.FromMinutes(5)
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/_health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async IAsyncEnumerable<string> SendMessageAsync(
        string content,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { content, channel = "cli" });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/messages")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null
               && !cancellationToken.IsCancellationRequested)
        {

            if (line.StartsWith("event: done", StringComparison.Ordinal))
                break;

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            var json = line[6..];
            if (string.IsNullOrEmpty(json) || json == "{}")
                continue;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var textElement))
            {
                var text = textElement.GetString();
                if (!string.IsNullOrEmpty(text))
                    yield return text;
            }
        }
    }

    public async Task<string?> GetTraceJsonAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/telemetry/traces/{requestId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string?> GetExplanationAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/telemetry/explain/{requestId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("explanation", out var exp) ? exp.GetString() : null;
    }

    public async Task<string?> GetCurrentMetricsJsonAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            "/api/v1/telemetry/metrics/summary", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
