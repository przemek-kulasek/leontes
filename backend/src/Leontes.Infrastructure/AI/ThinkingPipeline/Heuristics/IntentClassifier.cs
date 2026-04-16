using System.Text.RegularExpressions;

namespace Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

internal static class IntentClassifier
{
    private sealed record IntentRule(string Intent, Func<string, bool>[] Matchers);

    // Ordered by specificity: multi-word phrases and domain-specific intents
    // before generic single words. "?" is a fallback after all patterns checked.
    private static readonly IntentRule[] Rules = BuildRules(
    [
        ("search", ["find", "search", "look for", "locate", "where is", "show me"]),
        ("summarize", ["summarize", "summary", "tldr", "brief", "overview"]),
        ("remind", ["remind", "reminder", "don't forget", "remember to", "schedule"]),
        ("greeting", ["hello", "good morning", "good afternoon", "good evening"]),
        ("command", ["run", "execute", "start", "stop", "create", "delete", "send", "open", "close"]),
        ("question", ["what", "how", "why", "when", "where", "who", "which", "can you explain"]),
        ("greeting", ["hi", "hey"]),
    ]);

    public static string Classify(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "unknown";

        var lower = content.ToLowerInvariant();

        foreach (var rule in Rules)
        {
            foreach (var matcher in rule.Matchers)
            {
                if (matcher(lower))
                    return rule.Intent;
            }
        }

        // Fallback: question mark anywhere suggests a question
        if (lower.Contains('?'))
            return "question";

        return "conversation";
    }

    private static IntentRule[] BuildRules((string Intent, string[] Keywords)[] patterns)
    {
        return patterns.Select(p => new IntentRule(
            p.Intent,
            p.Keywords.Select(CreateMatcher).ToArray()
        )).ToArray();
    }

    private static Func<string, bool> CreateMatcher(string keyword)
    {
        if (keyword.Contains(' '))
        {
            // Multi-word phrase: plain substring match
            return input => input.Contains(keyword, StringComparison.Ordinal);
        }

        // Single word: word boundary regex to avoid "hi" matching inside "thinking"
        var regex = new Regex($@"\b{Regex.Escape(keyword)}\b", RegexOptions.Compiled);
        return input => regex.IsMatch(input);
    }
}
