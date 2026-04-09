namespace Leontes.Cli.Commands;

public static class InitCommand
{
    public static Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("Leontes Setup Wizard");
        Console.WriteLine("====================");
        Console.WriteLine();
        Console.WriteLine("This wizard will configure Leontes for first-time use.");
        Console.WriteLine();
        Console.WriteLine("Setup wizard is not yet implemented.");
        Console.WriteLine("Steps planned:");
        Console.WriteLine("  1. Start PostgreSQL via Docker Compose");
        Console.WriteLine("  2. Configure AI provider + API key");
        Console.WriteLine("  3. Register Signal bot");
        Console.WriteLine("  4. Set Sentinel defaults");
        Console.WriteLine("  5. Generate auth secrets");

        return Task.FromResult(0);
    }
}
