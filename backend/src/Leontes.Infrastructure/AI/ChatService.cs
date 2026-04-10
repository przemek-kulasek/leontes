using System.Runtime.CompilerServices;
using Leontes.Application;
using Leontes.Application.Chat;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI;

public sealed class ChatService(
    AIAgent _agent,
    IApplicationDbContext _db,
    ILogger<ChatService> _logger) : IChatService
{
    public async Task<Guid> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MessageChannel>(request.Channel, ignoreCase: true, out var channel))
        {
            throw new Domain.Exceptions.ValidationException("Invalid channel. Supported values: Cli, Signal.");
        }

        var conversation = await _db.Conversations
            .OrderByDescending(c => c.LastMessageAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Title = request.Content.Length > 50
                    ? string.Concat(request.Content.AsSpan(0, 47), "...")
                    : request.Content,
                LastMessageAt = DateTime.UtcNow
            };
            _db.Add(conversation);
        }
        else
        {
            conversation.LastMessageAt = DateTime.UtcNow;
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            Role = MessageRole.User,
            Content = request.Content,
            Channel = channel,
            ConversationId = conversation.Id
        };
        _db.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User message {MessageId} saved to conversation {ConversationId}", message.Id, conversation.Id);

        return conversation.Id;
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        Guid conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastUserMessage = await _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Role == MessageRole.User)
            .OrderByDescending(m => m.Created)
            .Select(m => m.Content)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException("No user message found for conversation.");

        _logger.LogDebug("Streaming AI response for conversation {ConversationId}", conversationId);

        var responseBuilder = new System.Text.StringBuilder();

        await foreach (var update in _agent.RunStreamingAsync(lastUserMessage, cancellationToken: cancellationToken))
        {
            var text = update.Text;
            if (!string.IsNullOrEmpty(text))
            {
                responseBuilder.Append(text);
                yield return text;
            }
        }

        var assistantMessage = new Message
        {
            Id = Guid.NewGuid(),
            Role = MessageRole.Assistant,
            Content = responseBuilder.ToString(),
            Channel = MessageChannel.Cli,
            ConversationId = conversationId,
            IsComplete = true
        };
        _db.Add(assistantMessage);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Assistant message {MessageId} saved to conversation {ConversationId}", assistantMessage.Id, conversationId);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        Guid conversationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.IsComplete)
            .OrderByDescending(m => m.Created)
            .Take(limit)
            .Select(m => new ChatMessageDto(m.Id, m.Role.ToString(), m.Content, m.Created))
            .ToListAsync(cancellationToken);
    }
}
