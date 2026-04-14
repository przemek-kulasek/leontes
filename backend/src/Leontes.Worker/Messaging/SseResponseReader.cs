using System.Text;
using System.Text.Json;

namespace Leontes.Worker.Messaging;

public static class SseResponseReader
{
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
}
