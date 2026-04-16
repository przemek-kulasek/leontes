using System.Text.Json;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;

namespace Leontes.Domain.Tests.ThinkingPipeline;

public sealed class ThinkingContextTests
{
    [Fact]
    public void ThinkingContext_Serialization_RoundTrips()
    {
        var context = new ThinkingContext
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            UserContent = "Find the report from last Tuesday",
            Channel = "Cli",
            Intent = "search",
            ExtractedEntities = ["last Tuesday", "report"],
            Urgency = MessageUrgency.High,
            RelevantMemories =
            [
                new RelevantMemory(Guid.NewGuid(), "Weekly report filed on Tuesdays", MemoryType.Episodic, 0.85)
            ],
            ConversationHistory =
            [
                new HistoryMessage("User", "Where are the reports?", DateTime.UtcNow.AddMinutes(-5))
            ],
            Plan = "Search for report files modified on Tuesday",
            SelectedTools = ["file_search"],
            Response = "Found 3 reports from last Tuesday.",
            IsComplete = true,
            NewInsights = ["User frequently asks about reports on Tuesdays"]
        };

        var json = JsonSerializer.Serialize(context);
        var deserialized = JsonSerializer.Deserialize<ThinkingContext>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(context.MessageId, deserialized.MessageId);
        Assert.Equal(context.ConversationId, deserialized.ConversationId);
        Assert.Equal(context.UserContent, deserialized.UserContent);
        Assert.Equal(context.Channel, deserialized.Channel);
        Assert.Equal(context.Intent, deserialized.Intent);
        Assert.Equal(context.Urgency, deserialized.Urgency);
        Assert.Equal(context.Plan, deserialized.Plan);
        Assert.Equal(context.Response, deserialized.Response);
        Assert.True(deserialized.IsComplete);
        Assert.Single(deserialized.RelevantMemories);
        Assert.Single(deserialized.ConversationHistory);
        Assert.Single(deserialized.SelectedTools);
        Assert.Single(deserialized.NewInsights);
    }

    [Fact]
    public void ThinkingContext_DefaultValues_AreEmpty()
    {
        var context = new ThinkingContext
        {
            MessageId = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            UserContent = "Hello",
            Channel = "Cli"
        };

        Assert.Null(context.Intent);
        Assert.Empty(context.ExtractedEntities);
        Assert.Equal(MessageUrgency.Normal, context.Urgency);
        Assert.Empty(context.RelevantMemories);
        Assert.Empty(context.ConversationHistory);
        Assert.Empty(context.ResolvedEntities);
        Assert.Null(context.Plan);
        Assert.Empty(context.SelectedTools);
        Assert.False(context.RequiresHumanInput);
        Assert.Null(context.HumanInputQuestion);
        Assert.Null(context.HumanInputResponse);
        Assert.Null(context.Response);
        Assert.False(context.IsComplete);
        Assert.Empty(context.ToolResults);
        Assert.Empty(context.NewInsights);
        Assert.Empty(context.GraphUpdates);
    }

    [Fact]
    public void RelevantMemory_Record_PreservesValues()
    {
        var id = Guid.NewGuid();
        var memory = new RelevantMemory(id, "test content", MemoryType.Semantic, 0.95);

        Assert.Equal(id, memory.MemoryId);
        Assert.Equal("test content", memory.Content);
        Assert.Equal(MemoryType.Semantic, memory.Type);
        Assert.Equal(0.95, memory.Relevance);
    }

    [Fact]
    public void ToolCallResult_Record_PreservesValues()
    {
        var result = new ToolCallResult("file_search", "*.pdf", "Found 3 files", true);

        Assert.Equal("file_search", result.ToolName);
        Assert.Equal("*.pdf", result.Input);
        Assert.Equal("Found 3 files", result.Output);
        Assert.True(result.Success);
    }
}
