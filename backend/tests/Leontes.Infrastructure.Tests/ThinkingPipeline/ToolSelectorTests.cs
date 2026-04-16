using Leontes.Infrastructure.AI.ThinkingPipeline.Heuristics;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class ToolSelectorTests
{
    [Fact]
    public void FromPlan_WithToolReferences_ExtractsTools()
    {
        var plan = "I'll use [tool:file_search] to find the document and [tool:calendar] to check the date.";

        var result = ToolSelector.FromPlan(plan);

        Assert.Equal(2, result.Count);
        Assert.Contains("file_search", result);
        Assert.Contains("calendar", result);
    }

    [Fact]
    public void FromPlan_WithoutToolReferences_ReturnsEmpty()
    {
        var plan = "I'll respond directly to the user's question.";

        var result = ToolSelector.FromPlan(plan);

        Assert.Empty(result);
    }

    [Fact]
    public void FromPlan_NullPlan_ReturnsEmpty()
    {
        Assert.Empty(ToolSelector.FromPlan(null));
    }

    [Fact]
    public void FromPlan_DuplicateTools_Deduplicates()
    {
        var plan = "First [tool:search], then process, then [tool:search] again.";

        var result = ToolSelector.FromPlan(plan);

        Assert.Single(result);
    }
}
