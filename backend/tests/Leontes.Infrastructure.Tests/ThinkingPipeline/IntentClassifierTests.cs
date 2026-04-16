using Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class IntentClassifierTests
{
    [Theory]
    [InlineData("What time is it?", "question")]
    [InlineData("How does this work?", "question")]
    [InlineData("Why did it fail?", "question")]
    public void Classify_QuestionPatterns_ReturnsQuestion(string content, string expected)
    {
        Assert.Equal(expected, IntentClassifier.Classify(content));
    }

    [Theory]
    [InlineData("Run the tests", "command")]
    [InlineData("Delete old files", "command")]
    [InlineData("Send the report to John", "command")]
    public void Classify_CommandPatterns_ReturnsCommand(string content, string expected)
    {
        Assert.Equal(expected, IntentClassifier.Classify(content));
    }

    [Theory]
    [InlineData("Find the report", "search")]
    [InlineData("Search for PDF files", "search")]
    [InlineData("Where is the config file?", "search")]
    public void Classify_SearchPatterns_ReturnsSearch(string content, string expected)
    {
        Assert.Equal(expected, IntentClassifier.Classify(content));
    }

    [Theory]
    [InlineData("Hello", "greeting")]
    [InlineData("Hi there!", "greeting")]
    [InlineData("Good morning", "greeting")]
    public void Classify_GreetingPatterns_ReturnsGreeting(string content, string expected)
    {
        Assert.Equal(expected, IntentClassifier.Classify(content));
    }

    [Fact]
    public void Classify_EmptyContent_ReturnsUnknown()
    {
        Assert.Equal("unknown", IntentClassifier.Classify(""));
    }

    [Fact]
    public void Classify_NoMatchingPattern_ReturnsConversation()
    {
        Assert.Equal("conversation", IntentClassifier.Classify("I was thinking about our project"));
    }
}
