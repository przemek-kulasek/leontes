using System.Text.RegularExpressions;
using Leontes.Application.Sentinel;

namespace Leontes.Infrastructure.Sentinel;

public sealed partial class ClipboardContentFilter : ISentinelFilter
{
    public const string Source = "Clipboard";

    public string MonitorSource => Source;

    public SentinelEvent? Evaluate(string rawEvent, IReadOnlyDictionary<string, string> metadata)
    {
        if (string.IsNullOrWhiteSpace(rawEvent))
            return null;

        var text = rawEvent.Trim();
        var now = DateTime.UtcNow;

        if (IbanPattern().IsMatch(text))
            return Classify("iban", "Bank account number copied", SentinelPriority.Medium, now, metadata);

        if (EmailPattern().IsMatch(text))
            return Classify("email", "Email address copied", SentinelPriority.Low, now, metadata);

        if (UrlPattern().IsMatch(text))
            return Classify("url", "URL copied", SentinelPriority.Low, now, metadata);

        if (LooksLikeStructuredData(text))
            return Classify("structured", "Structured data copied", SentinelPriority.Low, now, metadata);

        if (LooksLikeCredential(text))
            return Classify("credential", "Possible credential copied", SentinelPriority.High, now, metadata);

        return null;
    }

    private static SentinelEvent Classify(
        string pattern,
        string summary,
        SentinelPriority priority,
        DateTime occurredAt,
        IReadOnlyDictionary<string, string> metadata)
    {
        var meta = new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        return new SentinelEvent(
            MonitorSource: Source,
            EventType: "clipboard",
            Pattern: pattern,
            Summary: summary,
            Metadata: meta,
            OccurredAt: occurredAt,
            Priority: priority);
    }

    private static bool LooksLikeStructuredData(string text)
    {
        if (text.Length < 4)
            return false;

        var first = text[0];
        var last = text[^1];
        return (first == '{' && last == '}')
            || (first == '[' && last == ']')
            || (first == '<' && last == '>');
    }

    private static bool LooksLikeCredential(string text)
    {
        if (text.Length < 12 || text.Contains(' '))
            return false;

        var hasLower = false;
        var hasUpper = false;
        var hasDigit = false;
        var hasSymbol = false;

        foreach (var c in text)
        {
            if (char.IsLower(c)) hasLower = true;
            else if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else if (!char.IsWhiteSpace(c)) hasSymbol = true;
        }

        var categories = (hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0);
        return categories >= 3;
    }

    [GeneratedRegex(@"^[A-Z]{2}\d{2}[A-Z0-9]{10,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex IbanPattern();

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^https?://[^\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();
}
