using System.Net;
using System.Text;
using Leontes.Worker.Signal;

namespace Leontes.Worker.Tests;

public sealed class SignalMessageHelperTests
{
    [Fact]
    public void SplitMessage_ShortMessage_ReturnsSingleChunk()
    {
        var result = SignalMessageHelper.SplitMessage("Hello, world!");

        Assert.Single(result);
        Assert.Equal("Hello, world!", result[0]);
    }

    [Fact]
    public void SplitMessage_ExactlyAtLimit_ReturnsSingleChunk()
    {
        var text = new string('a', 2000);

        var result = SignalMessageHelper.SplitMessage(text);

        Assert.Single(result);
        Assert.Equal(2000, result[0].Length);
    }

    [Fact]
    public void SplitMessage_LongMessage_SplitsAtSentenceBoundary()
    {
        var sentence = "This is a test sentence. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 100));

        var result = SignalMessageHelper.SplitMessage(text);

        Assert.True(result.Count > 1);
        Assert.All(result, chunk => Assert.True(chunk.Length <= 2000));

        foreach (var chunk in result)
            Assert.EndsWith(".", chunk.TrimEnd());
    }

    [Fact]
    public void SplitMessage_LongMessageNoSentences_SplitsAtWordBoundary()
    {
        var word = "word ";
        var text = string.Concat(Enumerable.Repeat(word, 500));

        var result = SignalMessageHelper.SplitMessage(text);

        Assert.True(result.Count > 1);
        Assert.All(result, chunk => Assert.True(chunk.Length <= 2000));
    }

    [Fact]
    public void SplitMessage_EmptyString_ReturnsSingleEmptyChunk()
    {
        var result = SignalMessageHelper.SplitMessage("");

        Assert.Single(result);
        Assert.Equal("", result[0]);
    }

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

        var result = await SignalMessageHelper.ReadSseResponseAsync(response, CancellationToken.None);

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

        var result = await SignalMessageHelper.ReadSseResponseAsync(response, CancellationToken.None);

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

        var result = await SignalMessageHelper.ReadSseResponseAsync(response, CancellationToken.None);

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

        var result = await SignalMessageHelper.ReadSseResponseAsync(response, CancellationToken.None);

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
