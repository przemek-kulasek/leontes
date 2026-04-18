using System.Text.Json;
using Leontes.Cli.Config;
using Leontes.Cli.Http;

namespace Leontes.Cli.Commands;

public static class TraceCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || !Guid.TryParse(args[0], out var requestId))
        {
            Console.Error.WriteLine("Usage: leontes trace <requestId> [--explain]");
            return 1;
        }

        var explain = args.Contains("--explain", StringComparer.OrdinalIgnoreCase);

        var config = new CliConfiguration();
        using var client = new LeontesApiClient(config.BaseUrl, config.ApiKey);

        if (explain)
        {
            var explanation = await client.GetExplanationAsync(requestId);
            if (explanation is null)
            {
                Console.Error.WriteLine($"No trace found for request {requestId}.");
                return 1;
            }
            Console.WriteLine(explanation);
            return 0;
        }

        var json = await client.GetTraceJsonAsync(requestId);
        if (json is null)
        {
            Console.Error.WriteLine($"No trace found for request {requestId}.");
            return 1;
        }

        PrintFormatted(json);
        return 0;
    }

    private static void PrintFormatted(string json)
    {
        using var doc = JsonDocument.Parse(json);
        Console.WriteLine(JsonSerializer.Serialize(
            doc.RootElement,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
