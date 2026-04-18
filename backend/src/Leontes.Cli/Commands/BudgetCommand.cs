using System.Text.Json;
using Leontes.Cli.Config;
using Leontes.Cli.Http;

namespace Leontes.Cli.Commands;

public static class BudgetCommand
{
    private const int BarWidth = 20;

    public static async Task<int> RunAsync(string[] args)
    {
        var config = new CliConfiguration();
        using var client = new LeontesApiClient(config.BaseUrl, config.ApiKey);

        var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "today";

        return sub switch
        {
            "today" => await ShowTodayAsync(client),
            "history" => await ShowHistoryAsync(client, args),
            "set" => await SetBudgetAsync(client, args),
            _ => ShowHelp()
        };
    }

    private static async Task<int> ShowTodayAsync(LeontesApiClient client)
    {
        var json = await client.GetBudgetTodayJsonAsync();
        if (json is null)
        {
            Console.WriteLine("No budget data available.");
            return 0;
        }

        using var doc = JsonDocument.Parse(json);
        PrintReport(doc.RootElement);
        return 0;
    }

    private static async Task<int> ShowHistoryAsync(LeontesApiClient client, string[] args)
    {
        var days = 7;
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--days" && int.TryParse(args[i + 1], out var parsed))
                days = parsed;
        }

        var json = await client.GetBudgetHistoryJsonAsync(days);
        if (string.IsNullOrEmpty(json))
        {
            Console.WriteLine("No history available.");
            return 0;
        }

        using var doc = JsonDocument.Parse(json);
        foreach (var day in doc.RootElement.EnumerateArray())
        {
            PrintReport(day);
            Console.WriteLine();
        }

        return 0;
    }

    private static async Task<int> SetBudgetAsync(LeontesApiClient client, string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out var budget) || budget <= 0)
        {
            Console.Error.WriteLine("Usage: leontes budget set <tokens>");
            return 1;
        }

        var result = await client.SetDailyBudgetAsync(budget);
        if (result is null)
        {
            Console.Error.WriteLine("Failed to update budget.");
            return 1;
        }

        Console.WriteLine($"Daily token budget set to {budget:N0}.");
        return 0;
    }

    private static void PrintReport(JsonElement report)
    {
        var total = GetInt(report, "totalTokensUsed");
        var budget = GetInt(report, "dailyBudget");
        var percent = GetDouble(report, "percentUsed");
        var date = GetString(report, "date");

        Console.WriteLine($"Date: {date:O}");
        Console.WriteLine(
            $"Budget: {total:N0} / {budget:N0} tokens ({percent:F1}%) {Bar(percent)}");
        Console.WriteLine($"          State: {StateLabel(percent)}");

        if (report.TryGetProperty("byFeature", out var byFeature) &&
            byFeature.ValueKind == JsonValueKind.Object)
        {
            Console.WriteLine();
            foreach (var entry in byFeature.EnumerateObject())
            {
                var feature = entry.Name;
                var input = GetInt(entry.Value, "inputTokens");
                var output = GetInt(entry.Value, "outputTokens");
                var featureTotal = input + output;
                var featurePercent = budget == 0 ? 0 : featureTotal * 100.0 / budget;
                var calls = GetInt(entry.Value, "callCount");
                var model = GetString(entry.Value, "primaryModel");
                Console.WriteLine(
                    $"  {feature,-14} {featureTotal,10:N0} ({featurePercent,4:F1}%)  {Bar(featurePercent)}  {calls} calls • {model}");
            }
        }

        if (report.TryGetProperty("estimatedCostUsd", out var costElement) &&
            costElement.ValueKind == JsonValueKind.Number)
        {
            Console.WriteLine();
            Console.WriteLine($"Est. cost: ${costElement.GetDecimal():F4}");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("Est. cost: $0.00 (local model)");
        }
    }

    private static string Bar(double percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        var filled = (int)Math.Round(clamped / 100.0 * BarWidth);
        return "[" + new string('#', filled) + new string('.', BarWidth - filled) + "]";
    }

    private static string StateLabel(double percent) => percent switch
    {
        >= 100 => "Exhausted",
        >= 90 => "Throttled",
        >= 75 => "Warning",
        _ => "Normal"
    };

    private static int GetInt(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    private static double GetDouble(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    private static string GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "-" : "-";

    private static int ShowHelp()
    {
        Console.WriteLine("""
            Usage:
              leontes budget                Show today's usage
              leontes budget today          Show today's usage
              leontes budget history        Show last 7 days
              leontes budget history --days N
              leontes budget set <tokens>   Update the daily token budget
            """);
        return 0;
    }
}
