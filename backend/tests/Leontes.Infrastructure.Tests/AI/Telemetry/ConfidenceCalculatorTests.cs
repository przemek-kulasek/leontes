using Leontes.Application.Configuration;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.Telemetry;

namespace Leontes.Infrastructure.Tests.AI.Telemetry;

public sealed class ConfidenceCalculatorTests
{
    [Fact]
    public void Calculate_WithNoEvidence_ReturnsLowButNonZero()
    {
        var context = NewContext();

        var score = ConfidenceCalculator.Calculate(context, new ThinkingPipelineOptions());

        Assert.InRange(score.Overall, 0.0, 1.0);
        Assert.Equal(0.0, score.Breakdown.MemorySupport);
    }

    [Fact]
    public void Calculate_WithHighRelevanceMemoriesAndResolvedEntities_ReturnsHighConfidence()
    {
        var context = NewContext("What is Project Alpha status on the new launch?");
        context.Intent = "ask";
        context.ExtractedEntities = ["Project Alpha"];
        context.ResolvedEntities = [new ResolvedEntity("Project Alpha", Guid.NewGuid(), "Project", "Project Alpha")];
        context.RelevantMemories =
        [
            new RelevantMemory(Guid.NewGuid(), "meeting notes", MemoryType.Observation, 0.95, DateTime.UtcNow),
            new RelevantMemory(Guid.NewGuid(), "spec", MemoryType.Insight, 0.9, DateTime.UtcNow),
            new RelevantMemory(Guid.NewGuid(), "email", MemoryType.Observation, 0.8, DateTime.UtcNow),
        ];
        context.ToolResults = [new ToolCallResult("Search", "q", "r", Success: true)];

        var score = ConfidenceCalculator.Calculate(context, new ThinkingPipelineOptions());

        Assert.True(score.Overall >= 0.8, $"Expected >= 0.8, got {score.Overall:F2}");
    }

    [Fact]
    public void Calculate_WhenHumanInputRequired_PenalisesClarity()
    {
        var context = NewContext();
        context.RequiresHumanInput = true;
        context.Intent = "ask";

        var score = ConfidenceCalculator.Calculate(context, new ThinkingPipelineOptions());

        Assert.Equal(0.3, score.Breakdown.ConversationClarity);
    }

    [Fact]
    public void Calculate_WhenAllToolsFail_ZeroToolReliability()
    {
        var context = NewContext();
        context.ToolResults =
        [
            new ToolCallResult("A", "q", "", Success: false),
            new ToolCallResult("B", "q", "", Success: false)
        ];

        var score = ConfidenceCalculator.Calculate(context, new ThinkingPipelineOptions());

        Assert.Equal(0.0, score.Breakdown.ToolReliability);
    }

    private static ThinkingContext NewContext(string userContent = "hello") => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = userContent,
        Channel = "Cli"
    };
}
