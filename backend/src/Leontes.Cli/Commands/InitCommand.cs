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
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"user-secrets set \"{key}\" \"{value}\" --id {userSecretsId}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
                return false;

            process.WaitForExit(TimeSpan.FromSeconds(10));
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
