using Leontes.Cli.Http;

namespace Leontes.Cli.Commands;

public static class ChatCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("Leontes Chat");
        Console.WriteLine("Type a message and press Enter. Type 'exit' to quit.");
        Console.WriteLine();

        using var client = new LeontesApiClient();

        var healthy = await client.HealthCheckAsync();
        if (!healthy)
        {
            Console.Error.WriteLine("Cannot connect to Leontes API at http://localhost:5000");
            Console.Error.WriteLine("Make sure the API is running: dotnet run --project backend/src/Leontes.Api");
            return 1;
        }

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            try
            {
                await foreach (var chunk in client.SendMessageAsync(input))
                {
                    Console.Write(chunk);
                }
                Console.WriteLine();
                Console.WriteLine();
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
            }
        }

        return 0;
    }
}
