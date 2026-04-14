namespace Leontes.Application.Messaging;

public static class MessageSplitter
{
    public static IReadOnlyList<string> Split(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return [text];

        var chunks = new List<string>();
        var remaining = text.AsSpan();

        while (remaining.Length > 0)
        {
            if (remaining.Length <= maxLength)
            {
                chunks.Add(remaining.ToString());
                break;
            }

            var splitAt = FindSentenceBoundary(remaining, maxLength);
            chunks.Add(remaining[..splitAt].ToString());
            remaining = remaining[splitAt..].TrimStart();
        }

        return chunks;
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
