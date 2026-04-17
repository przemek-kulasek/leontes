using System.Diagnostics;
using Leontes.Cli.Auth;
using Leontes.Cli.Config;
using Leontes.Cli.Http;

namespace Leontes.Cli.Commands;

public static class InitCommand
{
    private const string ApiUserSecretsId = "e2ff695e-dd85-4dd3-ad70-14acaad2cf2d";
    private const string WorkerUserSecretsId = "7079f596-dc6d-426f-8d37-dacd99b8648a";
    private const string CliUserSecretsId = "b3a7f1d2-8c4e-4a9b-9f6d-2e5c8b1a0d3f";

    private const string ModelsKeyPrefix = "AiProvider:Models";
    private const string DefaultOllamaProvider = "ollama";
    private const string DefaultOllamaEndpoint = "http://localhost:11434";
    private const string DefaultLargeModelId = "qwen2.5:7b";
    private const string DefaultSmallModelId = "qwen2.5:3b";

    private static readonly string[] SupportedProviders = ["ollama"];

    public static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("Leontes Setup Wizard");
        Console.WriteLine("====================");
        Console.WriteLine();

        var apiKey = ApiKeyGenerator.Generate();

        Console.Write("Generating API key... ");
        var secretsSet = SetApiKeySecrets(apiKey);

        if (!secretsSet)
        {
            Console.WriteLine("FAILED");
            Console.WriteLine();
            Console.WriteLine("Could not set user secrets. Make sure the .NET SDK is installed.");
            return 1;
        }

        Console.WriteLine("OK");
        Console.WriteLine();
        Console.WriteLine("API key has been configured for all projects.");
        Console.WriteLine($"Key: {apiKey}");
        Console.WriteLine();

        if (!ConfigureAiModels())
        {
            Console.WriteLine();
            Console.WriteLine("Could not store AI model configuration in user secrets.");
            return 1;
        }

        var config = new CliConfiguration();
        using var client = new LeontesApiClient(config.BaseUrl, config.ApiKey);

        Console.Write("Checking API connection... ");
        var healthy = await client.HealthCheckAsync();

        if (healthy)
        {
            Console.WriteLine("OK");
            Console.WriteLine();
            Console.WriteLine("Leontes API is running and healthy.");
            Console.WriteLine("You can start chatting: leontes chat");
        }
        else
        {
            Console.WriteLine("NOT RUNNING");
            Console.WriteLine();
            Console.WriteLine("API key is configured. Start the API when ready:");
            Console.WriteLine("  1. Start PostgreSQL: docker compose up -d db");
            Console.WriteLine("  2. Start the API:    dotnet run --project backend/src/Leontes.Api");
            Console.WriteLine("  3. Ensure Ollama is running with your configured model");
        }

        return 0;
    }

    private static bool ConfigureAiModels()
    {
        Console.WriteLine("AI Provider Configuration");
        Console.WriteLine("-------------------------");
        Console.WriteLine("Large tier drives Plan + Execute. Small tier drives Reflect, Consolidation, Sentinel.");
        Console.WriteLine("Press Enter to accept each default.");
        Console.WriteLine();

        var large = PromptForTier("Large", DefaultLargeModelId);
        var small = PromptForTier("Small", DefaultSmallModelId);

        var settings = large.ToSecrets("Large").Concat(small.ToSecrets("Small"));

        foreach (var (key, value) in settings)
        {
            if (!SetUserSecret(ApiUserSecretsId, key, value))
                return false;
        }

        Console.WriteLine();
        Console.WriteLine("AI model configuration saved.");
        return true;
    }

    private static ModelTierInput PromptForTier(string tierLabel, string defaultModelId) => new(
        PromptForProvider(tierLabel),
        ConsolePrompt.AskWithDefault($"{tierLabel} model ID", defaultModelId),
        ConsolePrompt.AskWithDefault($"{tierLabel} endpoint", DefaultOllamaEndpoint));

    private static string PromptForProvider(string tierLabel)
    {
        while (true)
        {
            var value = ConsolePrompt.AskWithDefault($"{tierLabel} model provider", DefaultOllamaProvider);

            if (SupportedProviders.Contains(value, StringComparer.OrdinalIgnoreCase))
                return value.ToLowerInvariant();

            Console.WriteLine($"Unsupported provider '{value}'. Supported: {string.Join(", ", SupportedProviders)}.");
        }
    }

    private sealed record ModelTierInput(string Provider, string ModelId, string Endpoint)
    {
        public IEnumerable<(string Key, string Value)> ToSecrets(string tier) =>
        [
            ($"{ModelsKeyPrefix}:{tier}:Provider", Provider),
            ($"{ModelsKeyPrefix}:{tier}:ModelId", ModelId),
            ($"{ModelsKeyPrefix}:{tier}:Endpoint", Endpoint)
        ];
    }

    private static bool SetApiKeySecrets(string apiKey)
    {
        var secretIds = new[] { ApiUserSecretsId, WorkerUserSecretsId, CliUserSecretsId };

        foreach (var secretId in secretIds)
        {
            if (!SetUserSecret(secretId, "Authentication:ApiKey", apiKey))
                return false;
        }

        return true;
    }

    private static bool SetUserSecret(string userSecretsId, string key, string value)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("user-secrets");
            startInfo.ArgumentList.Add("set");
            startInfo.ArgumentList.Add(key);
            startInfo.ArgumentList.Add(value);
            startInfo.ArgumentList.Add("--id");
            startInfo.ArgumentList.Add(userSecretsId);

            using var process = Process.Start(startInfo);

            if (process is null)
                return false;

            if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
