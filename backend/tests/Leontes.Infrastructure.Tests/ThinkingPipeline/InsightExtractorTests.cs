using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class InsightExtractorTests
{
    [Fact]
    public void Extract_WithSuccessfulToolCalls_RecordsInsights()
    {
        var context = CreateContext();
        context.Intent = "search";
        context.ToolResults =
        [
            new ToolCallResult("file_search", "*.pdf", "Found 3 files", true)
        ];

        var insights = InsightExtractor.Extract(context);

        Assert.Single(insights);
        Assert.Contains("file_search", insights[0]);
        Assert.Contains("search", insights[0]);
    }

    [Fact]
    public void Extract_WithResolvedEntities_RecordsInsights()
    {
        var context = CreateContext();
        context.ResolvedEntities =
        [
            new ResolvedEntity("john", Guid.NewGuid(), "Person", "John Smith")
        ];

        var insights = InsightExtractor.Extract(context);

        Assert.Single(insights);
        Assert.Contains("john", insights[0]);
        Assert.Contains("John Smith", insights[0]);
    }

    [Fact]
    public void Extract_NoToolsNoEntities_ReturnsEmpty()
    {
        var context = CreateContext();

        var insights = InsightExtractor.Extract(context);

        Assert.Empty(insights);
    }

    [Fact]
    public void Extract_FailedToolCall_NotRecorded()
    {
        var context = CreateContext();
        context.ToolResults =
        [
            new ToolCallResult("web_search", "query", "Error: timeout", false)
        ];

        var insights = InsightExtractor.Extract(context);

        Assert.Empty(insights);
    }

    private static ThinkingContext CreateContext() => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = "test message",
        Channel = "Cli"
    };
}
