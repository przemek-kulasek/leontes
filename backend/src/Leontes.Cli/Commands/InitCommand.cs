using Leontes.Cli.Http;

namespace Leontes.Cli.Commands;

public static class InitCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("Leontes Setup Wizard");
        Console.WriteLine("====================");
        Console.WriteLine();

        using var client = new LeontesApiClient();

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
            Console.WriteLine("FAILED");
            Console.WriteLine();
            Console.WriteLine("Could not connect to the Leontes API.");
            Console.WriteLine("Make sure to:");
            Console.WriteLine("  1. Start PostgreSQL: docker compose up -d db");
            Console.WriteLine("  2. Start the API:    dotnet run --project backend/src/Leontes.Api");
            Console.WriteLine("  3. Ensure Ollama is running with your configured model");
            return 1;
        }

        return 0;
    }
}
