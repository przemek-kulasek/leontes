namespace Leontes.Cli.Config;

public static class ConsolePrompt
{
    public static string AskWithDefault(string label, string defaultValue)
        => AskWithDefault(Console.In, Console.Out, label, defaultValue);

    public static string AskWithDefault(
        TextReader reader,
        TextWriter writer,
        string label,
        string defaultValue)
    {
        writer.Write($"{label} [{defaultValue}]: ");
        writer.Flush();
        var input = reader.ReadLine();

        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }
}
