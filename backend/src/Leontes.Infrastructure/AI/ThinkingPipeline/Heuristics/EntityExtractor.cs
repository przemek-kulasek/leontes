using System.Text.RegularExpressions;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

internal static partial class EntityExtractor
{
    public static IReadOnlyList<string> Extract(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var entities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract quoted strings as potential entity references
        foreach (Match match in QuotedStringPattern().Matches(content))
        {
            var value = match.Groups[1].Value.Trim();
            if (value.Length is >= 2 and <= 100)
                entities.Add(value);
        }

        // Extract @mentions
        foreach (Match match in MentionPattern().Matches(content))
        {
            entities.Add(match.Groups[1].Value);
        }

        // Extract file paths (Windows and Unix)
        foreach (Match match in FilePathPattern().Matches(content))
        {
            entities.Add(match.Value);
        }

        // Extract URLs
        foreach (Match match in UrlPattern().Matches(content))
        {
            entities.Add(match.Value);
        }

        return [.. entities];
    }

    [GeneratedRegex("""["']([^"']+)["']""")]
    private static partial Regex QuotedStringPattern();

    [GeneratedRegex(@"@(\w+)")]
    private static partial Regex MentionPattern();

    [GeneratedRegex(@"(?:[A-Za-z]:\\|/)[\w./\\-]+")]
    private static partial Regex FilePathPattern();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex UrlPattern();
}
