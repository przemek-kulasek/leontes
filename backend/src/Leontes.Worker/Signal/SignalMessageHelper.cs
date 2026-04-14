using System.Text;
using System.Text.Json;

namespace Leontes.Worker.Signal;

public static class SignalMessageHelper
{
    public const int MaxSignalMessageLength = 2000;

    public static List<string> SplitMessage(string text)
    {
        if (text.Length <= MaxSignalMessageLength)
            return [text];

        var chunks = new List<string>();
        var remaining = text.AsSpan();

        while (remaining.Length > 0)
        {
            if (remaining.Length <= MaxSignalMessageLength)
            {
                chunks.Add(remaining.ToString());
                break;
            }

            var splitAt = FindSentenceBoundary(remaining, MaxSignalMessageLength);
            chunks.Add(remaining[..splitAt].ToString());
            remaining = remaining[splitAt..].TrimStart();
        }

        return chunks;
    }

    public static async Task<string> ReadSseResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var responseBuilder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
                break;

            if (line.StartsWith("event: done", StringComparison.Ordinal) ||
                line.StartsWith("event: error", StringComparison.Ordinal))
                break;

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var json = line["data: ".Length..];

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("text", out var textElement))
                        responseBuilder.Append(textElement.GetString());
                }
                catch (JsonException)
                {
                    // Skip malformed data lines
                }
            }
        }

        return responseBuilder.ToString();
    }

    private static int FindSentenceBoundary(ReadOnlySpan<char> text, int maxLength)
    {
        var searchArea = text[..maxLength];

        for (var i = searchArea.Length - 1; i >= searchArea.Length / 2; i--)
        {
            if (searchArea[i] is '.' or '!' or '?' or '\n')
                return i + 1;
        }

        for (var i = searchArea.Length - 1; i >= searchArea.Length / 2; i--)
        {
            if (searchArea[i] == ' ')
                return i + 1;
        }

        return maxLength;
    }
}
