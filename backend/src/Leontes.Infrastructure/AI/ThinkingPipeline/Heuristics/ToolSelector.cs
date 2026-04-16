using System.Text.RegularExpressions;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

internal static partial class ToolSelector
{
    public static IReadOnlyList<string> FromPlan(string? planText)
    {
        if (string.IsNullOrWhiteSpace(planText))
            return [];

        var tools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Look for tool references in the plan text: [tool:name] or tool:name
        foreach (Match match in ToolReferencePattern().Matches(planText))
        {
            tools.Add(match.Groups[1].Value);
        }

        return [.. tools];
    }

    [GeneratedRegex(@"\[?tool:(\w+)\]?", RegexOptions.IgnoreCase)]
    private static partial Regex ToolReferencePattern();
}
