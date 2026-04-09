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

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            Console.WriteLine("[Chat is not yet connected to the backend API]");
            Console.WriteLine();
        }

        await Task.CompletedTask;
        return 0;
    }
}
