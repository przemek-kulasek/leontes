using System.Text.RegularExpressions;
using Leontes.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.AI.Telemetry;

/// <summary>
/// Redacts values whose field or label matches any configured sensitive pattern.
/// Telemetry passes user-visible strings (questions, rationale, chosen labels) through here
/// before persistence — sensitive content is replaced with a placeholder.
/// </summary>
internal sealed class SensitiveDataRedactor
{
    private const string Placeholder = "[REDACTED]";

    private readonly Regex _combinedPattern;
    private readonly bool _hasPatterns;

    public SensitiveDataRedactor(IOptions<TelemetryOptions> options)
    {
        var patterns = options.Value.SensitiveFieldPatterns;
        _hasPatterns = patterns.Count > 0;

        _combinedPattern = _hasPatterns
            ? new Regex(
                string.Join("|", patterns.Select(p => Regex.Escape(p))),
                RegexOptions.IgnoreCase | RegexOptions.Compiled)
            : new Regex("(?!)");
    }

    public string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input) || !_hasPatterns)
            return input ?? string.Empty;

        return _combinedPattern.IsMatch(input) ? Placeholder : input;
    }
}
