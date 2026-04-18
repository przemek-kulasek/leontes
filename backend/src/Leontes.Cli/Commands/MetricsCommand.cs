using System.Text.Json;
using Leontes.Cli.Config;
using Leontes.Cli.Http;

namespace Leontes.Cli.Commands;

public static class MetricsCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        _ = args;

        var config = new CliConfiguration();
        using var client = new LeontesApiClient(config.BaseUrl, config.ApiKey);

        var json = await client.GetCurrentMetricsJsonAsync();
        if (json is null)
        {
            Console.WriteLine("No metrics summary is available yet.");
            return 0;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Console.WriteLine("Current metrics period");
        Console.WriteLine($"  Window        : {Get(root, "periodStart")} → {Get(root, "periodEnd")}");
        Console.WriteLine($"  Total         : {Get(root, "totalRequests")} requests");
        Console.WriteLine($"  Successful    : {Get(root, "successfulRequests")}");
        Console.WriteLine($"  Degraded      : {Get(root, "degradedRequests")}");
        Console.WriteLine($"  Failed        : {Get(root, "failedRequests")}");
        Console.WriteLine($"  Median latency: {Get(root, "medianLatencyMs")} ms");
        Console.WriteLine($"  P95 latency   : {Get(root, "p95LatencyMs")} ms");
        Console.WriteLine($"  Input tokens  : {Get(root, "totalInputTokens")}");
        Console.WriteLine($"  Output tokens : {Get(root, "totalOutputTokens")}");
        return 0;
    }

    private static string Get(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) ? v.ToString() : "-";
}
