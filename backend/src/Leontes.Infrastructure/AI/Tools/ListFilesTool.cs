using System.ComponentModel;

namespace Leontes.Infrastructure.AI.Tools;

public static class ListFilesTool
{
    private const int MaxResults = 100;

    [Description(
        "List files in a directory on the local machine. " +
        "Use when the user asks what files are in a folder or to find files by name. " +
        "Returns up to 100 entries; results beyond that limit are truncated.")]
    public static string ListFiles(
        [Description("Absolute or relative directory path to list.")] string path,
        [Description("Optional glob pattern (e.g. '*.cs', 'report-*.pdf'). Defaults to '*'.")] string? pattern = "*",
        [Description("Set to true to recurse into subdirectories. Defaults to false.")] bool recursive = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Error: path is required.";

        if (!Directory.Exists(path))
            return $"Error: directory not found: {path}";

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var effectivePattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern;

        try
        {
            var entries = Directory
                .EnumerateFileSystemEntries(path, effectivePattern, searchOption)
                .Take(MaxResults + 1)
                .ToList();

            if (entries.Count == 0)
                return $"No entries match '{effectivePattern}' in {path}.";

            var truncated = entries.Count > MaxResults;
            var visible = truncated ? entries.Take(MaxResults) : entries;
            var listing = string.Join('\n', visible);
            return truncated
                ? listing + $"\n[truncated — more than {MaxResults} entries]"
                : listing;
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: access denied to {path}.";
        }
        catch (IOException ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
