using Leontes.Application.Vision;

namespace Leontes.Application.Tests.Vision;

public sealed class ScreenIntentClassifierTests
{
    [Theory]
    [InlineData("what's on my screen?")]
    [InlineData("What am I looking at?")]
    [InlineData("read my screen and summarise the error")]
    [InlineData("what error is showing in this dialog?")]
    [InlineData("describe this window")]
    [InlineData("check my screen for typos")]
    public void RequiresScreenContext_WithScreenRelatedPhrase_ReturnsTrue(string input)
    {
        Assert.True(ScreenIntentClassifier.RequiresScreenContext(input));
    }

    [Theory]
    [InlineData("what's the weather today?")]
    [InlineData("summarise the meeting notes I emailed yesterday")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("    ")]
    public void RequiresScreenContext_WithoutScreenReference_ReturnsFalse(string? input)
    {
        Assert.False(ScreenIntentClassifier.RequiresScreenContext(input));
    }

    [Fact]
    public void RequiresScreenContext_IsCaseInsensitive()
    {
        Assert.True(ScreenIntentClassifier.RequiresScreenContext("WHAT IS ON MY SCREEN"));
    }
}
