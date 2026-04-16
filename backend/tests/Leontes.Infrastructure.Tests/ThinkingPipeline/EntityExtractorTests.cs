using Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class EntityExtractorTests
{
    [Fact]
    public void Extract_EmptyContent_ReturnsEmpty()
    {
        var result = EntityExtractor.Extract("");

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_NullContent_ReturnsEmpty()
    {
        var result = EntityExtractor.Extract(null!);

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_QuotedStrings_ExtractsEntities()
    {
        var result = EntityExtractor.Extract("Look for the \"quarterly report\" file");

        Assert.Contains("quarterly report", result);
    }

    [Fact]
    public void Extract_Mentions_ExtractsEntities()
    {
        var result = EntityExtractor.Extract("Send this to @john and @sarah");

        Assert.Contains("john", result);
        Assert.Contains("sarah", result);
    }

    [Fact]
    public void Extract_FilePaths_ExtractsEntities()
    {
        var result = EntityExtractor.Extract("Open the file at C:\\Users\\docs\\report.pdf");

        Assert.Contains(result, e => e.Contains("Users"));
    }

    [Fact]
    public void Extract_Urls_ExtractsEntities()
    {
        var result = EntityExtractor.Extract("Check https://example.com/report for details");

        Assert.Contains("https://example.com/report", result);
    }

    [Fact]
    public void Extract_PlainText_ReturnsEmpty()
    {
        var result = EntityExtractor.Extract("What is the weather today");

        Assert.Empty(result);
    }
}
