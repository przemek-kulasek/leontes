using Leontes.Application.Configuration;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Prompts;
using Microsoft.Extensions.AI;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class PlanningPromptBuilderTests
{
    private const string Persona = "You are Leontes.";
    private const double DefaultConfidence = 0.7;
    private const ProactivityLevel DefaultProactivity = ProactivityLevel.Balanced;

    [Fact]
    public void Build_MinimalContext_ReturnsSystemAndUserMessages()
    {
        var context = CreateContext("Hello");

        var messages = PlanningPromptBuilder.Build(context, Persona, DefaultConfidence, DefaultProactivity);

        Assert.Equal(2, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(ChatRole.User, messages[1].Role);
    }

    [Fact]
    public void Build_SystemPrompt_IncludesPlanningInstructions()
    {
        var context = CreateContext("Hello");

        var messages = PlanningPromptBuilder.Build(context, Persona, DefaultConfidence, DefaultProactivity);
        var systemText = messages[0].Text;

        Assert.Contains("PLANNING stage", systemText);
        Assert.Contains("[tool:toolName]", systemText);
        Assert.Contains("[NEEDS_CLARIFICATION]", systemText);
    }

    [Fact]
    public void Build_SystemPrompt_IncludesConfidenceThreshold()
    {
        var context = CreateContext("Hello");

        var messages = PlanningPromptBuilder.Build(context, Persona, 0.85, DefaultProactivity);
        var systemText = messages[0].Text;

        Assert.Contains("0.85", systemText);
        Assert.Contains("confidence threshold", systemText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ProactivityLevel.Minimal, "Only address what was asked")]
    [InlineData(ProactivityLevel.Balanced, "Surface relevant context")]
    [InlineData(ProactivityLevel.Proactive, "Actively suggest next steps")]
    public void Build_SystemPrompt_DescribesProactivityLevel(ProactivityLevel level, string expectedFragment)
    {
        var context = CreateContext("Hello");

        var messages = PlanningPromptBuilder.Build(context, Persona, DefaultConfidence, level);
        var systemText = messages[0].Text;

        Assert.Contains(level.ToString(), systemText);
        Assert.Contains(expectedFragment, systemText);
    }

    [Fact]
    public void Build_UserPrompt_IncludesIntentAndUrgency()
    {
        var context = CreateContext("Find the report");
        context.Intent = "search";
        context.Urgency = MessageUrgency.High;

        var messages = PlanningPromptBuilder.Build(context, Persona, DefaultConfidence, DefaultProactivity);
        var userText = messages[1].Text;

        Assert.Contains("search", userText);
        Assert.Contains("High", userText);
    }

    [Fact]
    public void Build_UserPrompt_IncludesExtractedEntities()
    {
        var context = CreateContext("Send to @john");
        context.ExtractedEntities = ["john"];

        var messages = PlanningPromptBuilder.Build(context, Persona, DefaultConfidence, DefaultProactivity);
        var userText = messages[1].Text;

        Assert.Contains("john", userText);
    }

    [Fact]
    public void Build_WithConversationHistory_IncludesHistoryMessages()
    {
        var context = CreateContext("Follow up");
        context.ConversationHistory =
        [
            new HistoryMessage("User", "First", DateTime.UtcNow.AddMinutes(-2)),
            new HistoryMessage("Assistant", "Reply", DateTime.UtcNow.AddMinutes(-1))
        ];

        var messages = PlanningPromptBuilder.Build(context, Persona, DefaultConfidence, DefaultProactivity);

        Assert.Equal(4, messages.Count);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Equal(ChatRole.Assistant, messages[2].Role);
    }

    [Fact]
    public void Build_WithRelevantMemories_IncludesMemoriesInUserPrompt()
    {
        var context = CreateContext("test");
        context.RelevantMemories =
        [
            new RelevantMemory(Guid.NewGuid(), "Past context", MemoryType.Episodic, 0.85)
        ];

        var messages = PlanningPromptBuilder.Build(context, Persona, DefaultConfidence, DefaultProactivity);
        var userText = messages[^1].Text;

        Assert.Contains("Past context", userText);
        Assert.Contains("0.85", userText);
    }

    [Fact]
    public void Build_UserPrompt_IncludesUserMessage()
    {
        var context = CreateContext("What time is it?");

        var messages = PlanningPromptBuilder.Build(context, Persona, DefaultConfidence, DefaultProactivity);
        var userText = messages[^1].Text;

        Assert.Contains("What time is it?", userText);
    }

    private static ThinkingContext CreateContext(string content) => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = content,
        Channel = "Cli"
    };
}
