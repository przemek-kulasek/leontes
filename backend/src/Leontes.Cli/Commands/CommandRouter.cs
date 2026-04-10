namespace Leontes.Cli.Commands;

public static class CommandRouter
{
    public static async Task<int> RunAsync(string[] args)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "chat";

        return command switch
        {
            "init" => await InitCommand.RunAsync(args[1..]),
            "chat" => await ChatCommand.RunAsync(args[1..]),
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
              leontes              Start interactive chat (default)
              leontes chat         Start interactive chat
              leontes init         Run first-time setup wizard

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
