using Leontes.Domain.Enums;
using Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class UrgencyDetectorTests
{
    [Theory]
    [InlineData("This is urgent!")]
    [InlineData("Emergency: server is down")]
    [InlineData("Critical bug in production")]
    [InlineData("Need this ASAP")]
    public void Detect_CriticalKeywords_ReturnsCritical(string content)
    {
        Assert.Equal(MessageUrgency.Critical, UrgencyDetector.Detect(content, "Cli"));
    }

    [Theory]
    [InlineData("This is important")]
    [InlineData("Please handle this soon")]
    [InlineData("High priority task")]
    public void Detect_HighKeywords_ReturnsHigh(string content)
    {
        Assert.Equal(MessageUrgency.High, UrgencyDetector.Detect(content, "Cli"));
    }

    [Theory]
    [InlineData("Whenever you can")]
    [InlineData("No rush on this")]
    [InlineData("Low priority fix")]
    public void Detect_LowKeywords_ReturnsLow(string content)
    {
        Assert.Equal(MessageUrgency.Low, UrgencyDetector.Detect(content, "Cli"));
    }

    [Fact]
    public void Detect_NormalMessage_ReturnsNormal()
    {
        Assert.Equal(MessageUrgency.Normal, UrgencyDetector.Detect("What is the weather?", "Cli"));
    }

    [Fact]
    public void Detect_EmptyContent_ReturnsNormal()
    {
        Assert.Equal(MessageUrgency.Normal, UrgencyDetector.Detect("", "Cli"));
    }
}
