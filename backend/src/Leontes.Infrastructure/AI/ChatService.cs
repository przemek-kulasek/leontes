using System.Runtime.CompilerServices;
using Leontes.Application;
using Leontes.Application.Chat;
using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Leontes.Domain.ThinkingPipeline;
using Leontes.Infrastructure.AI.ThinkingPipeline;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.AI;

public sealed class ChatService(
    ThinkingWorkflowHost _workflowHost,
    IWorkflowEventBridge _eventBridge,
    IApplicationDbContext _db,
    IServiceScopeFactory _scopeFactory,
    ILogger<ChatService> _logger) : IChatService
{
    public async Task<Guid> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<MessageChannel>(request.Channel, ignoreCase: true, out var channel))
        {
            throw new Domain.Exceptions.ValidationException("Invalid channel. Supported values: Cli, Signal, Telegram, Sentinel.");
        }

        Conversation? conversation = request.ConversationId.HasValue
            ? await _db.Conversations.FirstOrDefaultAsync(
                c => c.Id == request.ConversationId.Value, cancellationToken)
            : await _db.Conversations
                .OrderByDescending(c => c.LastMessageAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = request.ConversationId ?? Guid.NewGuid(),
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
        var lastUserMsg = await _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Role == MessageRole.User)
            .OrderByDescending(m => m.Created)
            .Select(m => new { m.Id, m.Content, m.Channel })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException("No user message found for conversation.");

        _logger.LogDebug("Starting thinking pipeline for conversation {ConversationId}", conversationId);

        // Build the ThinkingContext from the user message
        var thinkingContext = new ThinkingContext
        {
            MessageId = lastUserMsg.Id,
            ConversationId = conversationId,
            UserContent = lastUserMsg.Content,
            Channel = lastUserMsg.Channel.ToString()
        };

        // Register a client to capture token events from the pipeline.
        // The event bridge uses a bounded channel — UnregisterClient completes
        // the channel writer, which causes ReadEventsAsync to finish naturally.
        var clientId = $"chat-{conversationId}-{Guid.NewGuid():N}";
        _eventBridge.RegisterClient(clientId);

        ThinkingContext? result = null;

        try
        {
            // Start the pipeline in the background. When it finishes (success
            // or failure) it will have published all events; we then unregister
            // the client so ReadEventsAsync drains the remaining buffered events
            // and completes — no race, no lost tokens.
            var pipelineTask = _workflowHost.ProcessAsync(thinkingContext, cancellationToken)
                .ContinueWith(t =>
                {
                    _eventBridge.UnregisterClient(clientId);
                    return t;
                }, TaskScheduler.Default).Unwrap();

            // Stream tokens as they arrive — the channel will close when the
            // pipeline finishes and UnregisterClient completes the writer.
            await foreach (var evt in _eventBridge.ReadEventsAsync(clientId, cancellationToken))
            {
                if (evt is TokenStreamEvent tokenEvt)
                {
                    yield return tokenEvt.Text;
                }
            }

            // Pipeline already finished; await to propagate exceptions
            result = await pipelineTask;

            // Save the assistant message
            var assistantMessage = new Message
            {
                Id = Guid.NewGuid(),
                Role = MessageRole.Assistant,
                Content = result.Response ?? string.Empty,
                Channel = lastUserMsg.Channel,
                ConversationId = conversationId,
                IsComplete = result.IsComplete
            };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            db.Add(assistantMessage);
            await db.SaveChangesAsync(CancellationToken.None);

            _logger.LogInformation(
                "Assistant message {MessageId} saved to conversation {ConversationId}",
                assistantMessage.Id, conversationId);
        }
        finally
        {
            // Safety net: if ReadEventsAsync threw before the pipeline finished,
            // ensure the client is always cleaned up.
            _eventBridge.UnregisterClient(clientId);
        }
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        Guid conversationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.IsComplete && m.Role != MessageRole.Summary)
            .OrderByDescending(m => m.Created)
            .Take(limit)
            .Select(m => new ChatMessageDto(m.Id, m.Role.ToString(), m.Content, m.Created))
            .ToListAsync(cancellationToken);
    }
}
