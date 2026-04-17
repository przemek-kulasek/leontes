using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline.Prompts;
using Microsoft.Extensions.AI;

namespace Leontes.Infrastructure.Tests.ThinkingPipeline;

public sealed class ExecutionPromptBuilderTests
{
    private const string Persona = "You are Leontes.";

    [Fact]
    public void Build_MinimalContext_ReturnsSystemAndUserMessages()
    {
        var context = CreateContext("Hello");

        var messages = ExecutionPromptBuilder.Build(context, Persona);

        Assert.Equal(2, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Equal("Hello", messages[1].Text);
    }

    [Fact]
    public void Build_WithConversationHistory_IncludesHistoryMessages()
    {
        var context = CreateContext("Follow up question");
        context.ConversationHistory =
        [
            new HistoryMessage("User", "First message", DateTime.UtcNow.AddMinutes(-5)),
            new HistoryMessage("Assistant", "First reply", DateTime.UtcNow.AddMinutes(-4))
        ];

        var messages = ExecutionPromptBuilder.Build(context, Persona);

        Assert.Equal(4, messages.Count);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.Equal("First message", messages[1].Text);
        Assert.Equal(ChatRole.Assistant, messages[2].Role);
        Assert.Equal("First reply", messages[2].Text);
        Assert.Equal("Follow up question", messages[3].Text);
    }

    [Fact]
    public void Build_WithPlan_IncludesPlanInSystemPrompt()
    {
        var context = CreateContext("test");
        context.Plan = "Search for PDF files using file_search tool";

        var messages = ExecutionPromptBuilder.Build(context, Persona);
        var systemText = messages[0].Text;

        Assert.Contains("Execution Plan", systemText);
        Assert.Contains("Search for PDF files", systemText);
    }

    [Fact]
    public void Build_WithMemories_IncludesMemoriesInSystemPrompt()
    {
        var context = CreateContext("test");
        context.RelevantMemories =
        [
            new RelevantMemory(Guid.NewGuid(), "User prefers terse responses", MemoryType.Preference, 0.9, DateTime.UtcNow)
        ];

        var messages = ExecutionPromptBuilder.Build(context, Persona);
        var systemText = messages[0].Text;

        Assert.Contains("Relevant Context", systemText);
        Assert.Contains("User prefers terse responses", systemText);
    }

    [Fact]
    public void Build_WithResolvedEntities_IncludesEntityContextInSystemPrompt()
    {
        var context = CreateContext("test");
        context.ResolvedEntities =
        [
            new ResolvedEntity("john", Guid.NewGuid(), "Person", "John Smith")
        ];

        var messages = ExecutionPromptBuilder.Build(context, Persona);
        var systemText = messages[0].Text;

        Assert.Contains("Entity Context", systemText);
        Assert.Contains("John Smith", systemText);
    }

    [Fact]
    public void Build_WithHumanClarification_IncludesClarificationInSystemPrompt()
    {
        var context = CreateContext("test");
        context.HumanInputResponse = "I meant option B";

        var messages = ExecutionPromptBuilder.Build(context, Persona);
        var systemText = messages[0].Text;

        Assert.Contains("User Clarification", systemText);
        Assert.Contains("I meant option B", systemText);
    }

    [Fact]
    public void Build_SystemPromptStartsWithPersonaInstructions()
    {
        var context = CreateContext("test");

        var messages = ExecutionPromptBuilder.Build(context, Persona);

        Assert.StartsWith(Persona, messages[0].Text);
    }

    private static ThinkingContext CreateContext(string content) => new()
    {
        MessageId = Guid.NewGuid(),
        ConversationId = Guid.NewGuid(),
        UserContent = content,
        Channel = "Cli"
    };
}
