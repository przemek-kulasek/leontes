using Leontes.Domain.Entities;
using Leontes.Domain.Enums;

namespace Leontes.Domain.Tests.Entities;

public class ConversationTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_CreatesConversation()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var conversation = new Conversation
        {
            Id = id,
            Title = "Test conversation",
            LastMessageAt = now
        };

        Assert.Equal(id, conversation.Id);
        Assert.Equal("Test conversation", conversation.Title);
        Assert.Equal(now, conversation.LastMessageAt);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void Messages_DefaultsToEmptyCollection()
    {
        var conversation = new Conversation
        {
            Title = "Empty",
            LastMessageAt = DateTime.UtcNow
        };

        Assert.NotNull(conversation.Messages);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void InitiatedBy_DefaultsToUser()
    {
        var conversation = new Conversation
        {
            Title = "Test",
            LastMessageAt = DateTime.UtcNow
        };

        Assert.Equal(MessageInitiator.User, conversation.InitiatedBy);
    }

    [Fact]
    public void IsProactive_DefaultsToFalse()
    {
        var conversation = new Conversation
        {
            Title = "Test",
            LastMessageAt = DateTime.UtcNow
        };

        Assert.False(conversation.IsProactive);
    }

    [Fact]
    public void ProactiveConversation_CanBeCreated()
    {
        var conversation = new Conversation
        {
            Title = "Sentinel Alert",
            LastMessageAt = DateTime.UtcNow,
            InitiatedBy = MessageInitiator.Sentinel,
            IsProactive = true
        };

        Assert.Equal(MessageInitiator.Sentinel, conversation.InitiatedBy);
        Assert.True(conversation.IsProactive);
    }
}
