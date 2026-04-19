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

        if (!ConfigureSentinel())
        {
            Console.WriteLine();
            Console.WriteLine("Could not store Sentinel configuration in user secrets.");
            return 1;
        }

        if (!ConfigureVision())
        {
            Console.WriteLine();
            Console.WriteLine("Could not store Structural Vision configuration in user secrets.");
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

    private static bool ConfigureSentinel()
    {
        Console.WriteLine();
        Console.WriteLine("Sentinel Configuration");
        Console.WriteLine("----------------------");
        Console.WriteLine("Sentinel watches OS events (files, clipboard) and escalates surprising ones to the AI.");
        Console.WriteLine("Press Enter to accept each default.");
        Console.WriteLine();

        var clipboard = ConsolePrompt.AskWithDefault("Enable clipboard monitor (y/n)", "y");
        var fileSystem = ConsolePrompt.AskWithDefault("Enable file system monitor (y/n)", "y");

        var defaultDownloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        var watchInput = ConsolePrompt.AskWithDefault(
            "File system watch paths (semicolon-separated)",
            defaultDownloads);

        var settings = new List<(string Key, string Value)>
        {
            ("Sentinel:Enabled", "true"),
            ("Sentinel:Monitors:Clipboard:Enabled", IsYes(clipboard) ? "true" : "false"),
            ("Sentinel:Monitors:FileSystem:Enabled", IsYes(fileSystem) ? "true" : "false")
        };

        var paths = watchInput.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < paths.Length; i++)
            settings.Add(($"Sentinel:Monitors:FileSystem:WatchPaths:{i}", paths[i]));

        foreach (var (key, value) in settings)
        {
            if (!SetUserSecret(WorkerUserSecretsId, key, value))
                return false;
        }

        Console.WriteLine();
        Console.WriteLine("Sentinel configuration saved.");
        return true;
    }

    private static bool ConfigureVision()
    {
        Console.WriteLine();
        Console.WriteLine("Structural Vision Configuration");
        Console.WriteLine("-------------------------------");
        Console.WriteLine("Structural Vision reads the focused window's UI Automation tree so the");
        Console.WriteLine("assistant can answer questions about what is on your screen.");
        Console.WriteLine("No screenshots or OCR. Disabled by default.");
        Console.WriteLine();

        var enabled = ConsolePrompt.AskWithDefault("Enable Structural Vision (y/n)", "n");
        var enabledYes = IsYes(enabled);

        var settings = new List<(string Key, string Value)>
        {
            ("Vision:Enabled", enabledYes ? "true" : "false"),
            ("Vision:RequireExplicitRequest", "true"),
            ("Vision:ExcludePasswordFields", "true")
        };

        if (!ClearUserSecretsWithPrefix(ApiUserSecretsId, "Vision:ExcludedProcesses:"))
            return false;

        if (enabledYes)
        {
            var defaultExclusions = "1password;keepass;bitwarden;lastpass;mstsc;vmconnect;msedge;chrome;firefox";
            var excludeInput = ConsolePrompt.AskWithDefault(
                "Excluded processes (semicolon-separated)",
                defaultExclusions);

            var processes = excludeInput.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < processes.Length; i++)
                settings.Add(($"Vision:ExcludedProcesses:{i}", processes[i]));
        }

        foreach (var (key, value) in settings)
        {
            if (!SetUserSecret(ApiUserSecretsId, key, value))
                return false;
        }

        Console.WriteLine();
        Console.WriteLine(enabledYes
            ? "Structural Vision enabled. Ask the assistant things like 'what error is on my screen?'"
            : "Structural Vision disabled. You can enable it later in appsettings or re-run 'leontes init'.");
        return true;
    }

    private static bool IsYes(string value) =>
        value.StartsWith('y') || value.StartsWith('Y');

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

    private static bool ClearUserSecretsWithPrefix(string userSecretsId, string keyPrefix)
    {
        var keys = ListUserSecretKeys(userSecretsId);
        if (keys is null)
            return false;

        foreach (var key in keys)
        {
            if (key.StartsWith(keyPrefix, StringComparison.Ordinal) &&
                !RemoveUserSecret(userSecretsId, key))
                return false;
        }

        return true;
    }

    private static IReadOnlyList<string>? ListUserSecretKeys(string userSecretsId)
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
            startInfo.ArgumentList.Add("list");
            startInfo.ArgumentList.Add("--id");
            startInfo.ArgumentList.Add(userSecretsId);

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            if (process.ExitCode != 0)
                return null;

            var keys = new List<string>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = line.IndexOf(" = ", StringComparison.Ordinal);
                if (separator > 0)
                    keys.Add(line[..separator]);
            }

            return keys;
        }
        catch
        {
            return null;
        }
    }

    private static bool RemoveUserSecret(string userSecretsId, string key)
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
            startInfo.ArgumentList.Add("remove");
            startInfo.ArgumentList.Add(key);
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
