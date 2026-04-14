using System.Net;
using System.Text;
using Leontes.Worker.Messaging;

namespace Leontes.Worker.Tests;

public sealed class SseResponseReaderTests
{
    [Fact]
    public async Task ReadSseResponseAsync_WithChunksAndDone_ReturnsFullText()
    {
        var sseContent = """
            event: chunk
            data: {"text":"Hello "}

            event: chunk
            data: {"text":"world!"}

            event: done
            data: {}

            """;

        var response = CreateSseResponse(sseContent);

        var result = await SseResponseReader.ReadSseResponseAsync(response, CancellationToken.None);

        Assert.Equal("Hello world!", result);
    }

    [Fact]
    public async Task ReadSseResponseAsync_WithErrorEvent_StopsReading()
    {
        var sseContent = """
            event: chunk
            data: {"text":"partial"}

            event: error
            data: {"message":"something broke"}

            event: chunk
            data: {"text":" should not appear"}

            """;

        var response = CreateSseResponse(sseContent);

        var result = await SseResponseReader.ReadSseResponseAsync(response, CancellationToken.None);

        Assert.Equal("partial", result);
    }

    [Fact]
    public async Task ReadSseResponseAsync_WithNoChunks_ReturnsEmpty()
    {
        var sseContent = """
            event: done
            data: {}

            """;

        var response = CreateSseResponse(sseContent);

        var result = await SseResponseReader.ReadSseResponseAsync(response, CancellationToken.None);

        Assert.Equal("", result);
    }

    [Fact]
    public async Task ReadSseResponseAsync_WithMalformedJson_SkipsLine()
    {
        var sseContent = """
            event: chunk
            data: not-json

            event: chunk
            data: {"text":"valid"}

            event: done
            data: {}

            """;

        var response = CreateSseResponse(sseContent);

        var result = await SseResponseReader.ReadSseResponseAsync(response, CancellationToken.None);

        Assert.Equal("valid", result);
    }

    private static HttpResponseMessage CreateSseResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/event-stream")
        };
    }
}
