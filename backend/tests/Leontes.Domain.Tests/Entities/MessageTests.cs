using Leontes.Domain.Entities;
using Leontes.Domain.Enums;

namespace Leontes.Domain.Tests.Entities;

public class MessageTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_CreatesMessage()
    {
        var id = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var message = new Message
        {
            Id = id,
            Role = MessageRole.User,
            Content = "Hello",
            Channel = MessageChannel.Cli,
            ConversationId = conversationId
        };

        Assert.Equal(id, message.Id);
        Assert.Equal(MessageRole.User, message.Role);
        Assert.Equal("Hello", message.Content);
        Assert.Equal(MessageChannel.Cli, message.Channel);
        Assert.Equal(conversationId, message.ConversationId);
    }

    [Fact]
    public void IsComplete_DefaultsToTrue()
    {
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Content = "Response",
            Channel = MessageChannel.Cli,
            ConversationId = Guid.NewGuid()
        };

        Assert.True(message.IsComplete);
    }

    [Fact]
    public void IsComplete_CanBeSetToFalse()
    {
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Content = "Partial",
            Channel = MessageChannel.Cli,
            ConversationId = Guid.NewGuid(),
            IsComplete = false
        };

        Assert.False(message.IsComplete);
    }

    [Fact]
    public void Initiator_DefaultsToUser()
    {
        var message = new Message
        {
            Role = MessageRole.User,
            Content = "Hello",
            Channel = MessageChannel.Cli,
            ConversationId = Guid.NewGuid()
        };

        Assert.Equal(MessageInitiator.User, message.Initiator);
    }

    [Fact]
    public void Initiator_CanBeSetToAssistant()
    {
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Content = "Proactive notification",
            Channel = MessageChannel.Cli,
            ConversationId = Guid.NewGuid(),
            Initiator = MessageInitiator.Assistant
        };

        Assert.Equal(MessageInitiator.Assistant, message.Initiator);
    }
}
