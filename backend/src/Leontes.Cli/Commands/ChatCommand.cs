using Leontes.Cli.Config;
using Leontes.Cli.Http;

namespace Leontes.Cli.Commands;

public static class ChatCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("Leontes Chat");
        Console.WriteLine("Type a message and press Enter. Type '/new' to start a new conversation, 'exit' to quit.");
        Console.WriteLine();

        var config = new CliConfiguration();
        using var client = new LeontesApiClient(config.BaseUrl, config.ApiKey);

        var healthy = await client.HealthCheckAsync();
        if (!healthy)
        {
            Console.Error.WriteLine($"Cannot connect to Leontes API at {config.BaseUrl}");
            Console.Error.WriteLine("Make sure the API is running: dotnet run --project backend/src/Leontes.Api");
            return 1;
        }

        var conversationId = Guid.NewGuid();

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals("/new", StringComparison.OrdinalIgnoreCase))
            {
                conversationId = Guid.NewGuid();
                Console.WriteLine("Started a new conversation.");
                Console.WriteLine();
                continue;
            }

            try
            {
                await foreach (var chunk in client.SendMessageAsync(input, conversationId))
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
