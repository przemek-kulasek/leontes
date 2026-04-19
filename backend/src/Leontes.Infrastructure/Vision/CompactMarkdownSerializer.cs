using System.Text;
using Leontes.Application.Vision;
using Leontes.Domain.Vision;

namespace Leontes.Infrastructure.Vision;

/// <summary>
/// Serializes a UI Automation tree into a token-efficient Markdown representation.
/// Enforces a max token budget by pruning deepest nodes first while preserving top-level structure.
/// </summary>
public sealed class CompactMarkdownSerializer : ITreeSerializer
{
    // Rough heuristic: ~4 chars/token for English text.
    private const int CharsPerToken = 4;

    // Appended at the end when the tree had to be truncated to fit the budget.
    private const string TruncationMarker = "  [... truncated to fit token budget ...]";

    public string Serialize(UIElement root, TreeSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var opts = options ?? new TreeSerializerOptions();

        var maxChars = opts.MaxTokenEstimate * CharsPerToken;

        var fullText = Render(root, opts);
        if (fullText.Length <= maxChars)
            return fullText;

        return TruncateToFit(root, opts, maxChars);
    }

    private static string Render(UIElement root, TreeSerializerOptions opts)
    {
        var sb = new StringBuilder();
        Append(sb, root, indent: 0, opts, maxDepth: int.MaxValue);
        return sb.ToString();
    }

    private static string TruncateToFit(UIElement root, TreeSerializerOptions opts, int maxChars)
    {
        // Progressively shrink depth until output fits. Always keep the root line visible.
        var budget = maxChars - TruncationMarker.Length - 1;

        for (var depth = MaxDepth(root); depth >= 0; depth--)
        {
            var sb = new StringBuilder();
            Append(sb, root, indent: 0, opts, maxDepth: depth);
            if (sb.Length <= budget)
            {
                sb.AppendLine();
                sb.Append(TruncationMarker);
                return sb.ToString();
            }
        }

        // Even the root alone is too large — hard-trim the single-line label.
        var rootLabel = FormatElement(root, opts);
        if (rootLabel.Length > maxChars)
            rootLabel = rootLabel.Substring(0, Math.Max(0, maxChars - 3)) + "...";
        return rootLabel;
    }

    private static void Append(
        StringBuilder sb,
        UIElement element,
        int indent,
        TreeSerializerOptions opts,
        int maxDepth)
    {
        var prefix = new string(' ', indent * 2);
        var label = FormatElement(element, opts);

        if (!string.IsNullOrEmpty(label))
            sb.Append(prefix).AppendLine(label);

        if (indent >= maxDepth)
            return;

        foreach (var child in element.Children)
            Append(sb, child, indent + 1, opts, maxDepth);
    }

    private static string FormatElement(UIElement element, TreeSerializerOptions opts)
    {
        var type = SimplifyControlType(element.ControlType);
        var name = element.Name ?? element.AutomationId;
        var value = element.Value is not null
            ? $" = \"{Truncate(element.Value, 100)}\""
            : "";

        var bounds = opts.IncludeBounds && element.BoundingRectangle is { } rect
            ? $" @({(int)rect.X},{(int)rect.Y},{(int)rect.Width}x{(int)rect.Height})"
            : "";

        var stateSuffix = element.IsEnabled ? "" : " (disabled)";

        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(element.Value))
            return $"[{type}]{bounds}{stateSuffix}";

        return $"[{type}: {name}{value}]{bounds}{stateSuffix}";
    }

    private static string SimplifyControlType(string controlType) =>
        controlType.StartsWith("ControlType.", StringComparison.Ordinal)
            ? controlType.Substring("ControlType.".Length)
            : controlType;

    private static string Truncate(string input, int max) =>
        input.Length <= max ? input : input.Substring(0, max) + "...";

    private static int MaxDepth(UIElement element)
    {
        if (element.Children.Count == 0) return 0;
        var deepest = 0;
        foreach (var child in element.Children)
            deepest = Math.Max(deepest, MaxDepth(child));
        return deepest + 1;
    }
}
