using Leontes.Application.Messaging;

namespace Leontes.Application.Tests;

public sealed class MessageSplitterTests
{
    [Fact]
    public void Split_ShortMessage_ReturnsSingleChunk()
    {
        var result = MessageSplitter.Split("Hello, world!", 2000);

        Assert.Single(result);
        Assert.Equal("Hello, world!", result[0]);
    }

    [Fact]
    public void Split_ExactlyAtLimit_ReturnsSingleChunk()
    {
        var text = new string('a', 4096);

        var result = MessageSplitter.Split(text, 4096);

        Assert.Single(result);
        Assert.Equal(4096, result[0].Length);
    }

    [Fact]
    public void Split_LongMessage_SplitsAtSentenceBoundary()
    {
        var sentence = "This is a test sentence. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 100));

        var result = MessageSplitter.Split(text, 2000);

        Assert.True(result.Count > 1);
        Assert.All(result, chunk => Assert.True(chunk.Length <= 2000));

        foreach (var chunk in result)
            Assert.EndsWith(".", chunk.TrimEnd());
    }

    [Fact]
    public void Split_LongMessageNoSentences_SplitsAtWordBoundary()
    {
        var word = "word ";
        var text = string.Concat(Enumerable.Repeat(word, 500));

        var result = MessageSplitter.Split(text, 2000);

        Assert.True(result.Count > 1);
        Assert.All(result, chunk => Assert.True(chunk.Length <= 2000));
    }

    [Fact]
    public void Split_EmptyString_ReturnsSingleEmptyChunk()
    {
        var result = MessageSplitter.Split("", 2000);

        Assert.Single(result);
        Assert.Equal("", result[0]);
    }

    [Fact]
    public void Split_TelegramLimit_RespectsLargerMaxLength()
    {
        var text = new string('a', 3000);

        var resultSignal = MessageSplitter.Split(text, 2000);
        var resultTelegram = MessageSplitter.Split(text, 4096);

        Assert.True(resultSignal.Count > 1);
        Assert.Single(resultTelegram);
    }

    [Fact]
    public void Split_DifferentLimits_ProduceDifferentChunkCounts()
    {
        var sentence = "This is a fairly long test sentence for splitting. ";
        var text = string.Concat(Enumerable.Repeat(sentence, 200));

        var resultSmall = MessageSplitter.Split(text, 2000);
        var resultLarge = MessageSplitter.Split(text, 4096);

        Assert.True(resultSmall.Count > resultLarge.Count);
    }
}
