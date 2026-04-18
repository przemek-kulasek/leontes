namespace Leontes.Cli.Commands;

public static class CommandRouter
{
    public static async Task<int> RunAsync(string[] args)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "chat";
        var remaining = args.Length > 1 ? args[1..] : [];

        return command switch
        {
            "init" => await InitCommand.RunAsync(remaining),
            "chat" => await ChatCommand.RunAsync(remaining),
            "trace" => await TraceCommand.RunAsync(remaining),
            "metrics" => await MetricsCommand.RunAsync(remaining),
            "budget" => await BudgetCommand.RunAsync(remaining),
            "--help" or "-h" => ShowHelp(),
            "--version" or "-v" => ShowVersion(),
            _ => ShowUnknownCommand(command)
        };
    }

    private static int ShowHelp()
    {
        Console.WriteLine("""
            leontes - Your proactive OS partner

            Usage:
              leontes                         Start interactive chat (default)
              leontes chat                    Start interactive chat
              leontes init                    Run first-time setup wizard
              leontes trace <id> [--explain]  Show the pipeline trace for a request
              leontes metrics                 Show the current metrics summary
              leontes budget [today|history|set <n>]  Inspect or update the token budget

            Options:
              --help, -h           Show this help message
              --version, -v        Show version information
            """);
        return 0;
    }

    private static int ShowVersion()
    {
        var version = typeof(CommandRouter).Assembly.GetName().Version;
        Console.WriteLine($"leontes {version}");
        return 0;
    }

    private static int ShowUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Run 'leontes --help' for usage information.");
        return 1;
    }
}
