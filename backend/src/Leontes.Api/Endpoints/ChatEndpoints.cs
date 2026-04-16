using System.Text.Json;
using Leontes.Application.Chat;

namespace Leontes.Api.Endpoints;

public static class ChatEndpoints
{
    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/messages", SendMessage)
            .WithName("SendMessage")
            .WithSummary("Send a message and stream the AI response via SSE")
            .WithTags("Chat")
            .Accepts<SendMessageRequest>("application/json")
            .Produces(200, contentType: "text/event-stream")
            .Produces(400);

        group.MapGet("/messages", GetMessages)
            .WithName("GetMessages")
            .WithSummary("Get message history for a conversation")
            .WithTags("Chat")
            .Produces<IReadOnlyList<ChatMessageDto>>()
            .Produces(400);

        return group;
    }

    private static async Task SendMessage(
        HttpContext context,
        SendMessageRequest request,
        IChatService chatService,
        CancellationToken cancellationToken)
    {
        var conversationId = await chatService.SendMessageAsync(request, cancellationToken);

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var chunk in chatService.StreamResponseAsync(conversationId, cancellationToken))
            {
                var data = JsonSerializer.Serialize(new { text = chunk });
                await context.Response.WriteAsync($"event: chunk\ndata: {data}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }

            await context.Response.WriteAsync("event: done\ndata: {}\n\n", CancellationToken.None);
            await context.Response.Body.FlushAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — attempt terminal event on best-effort basis
            try
            {
                await context.Response.WriteAsync("event: done\ndata: {}\n\n", CancellationToken.None);
                await context.Response.Body.FlushAsync(CancellationToken.None);
            }
            catch { /* client already gone */ }
        }
        catch (Exception)
        {
            try
            {
                var errorData = JsonSerializer.Serialize(new { message = "An error occurred while streaming the response." });
                await context.Response.WriteAsync($"event: error\ndata: {errorData}\n\n", CancellationToken.None);
                await context.Response.Body.FlushAsync(CancellationToken.None);
            }
            catch { /* client already gone */ }

            throw;
        }
    }

    private static async Task<IResult> GetMessages(
        IChatService chatService,
        Guid? conversationId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (conversationId is null)
            throw new Leontes.Domain.Exceptions.ValidationException("conversationId is required.");

        var messages = await chatService.GetMessagesAsync(conversationId.Value, limit, cancellationToken);
        return Results.Ok(messages);
    }
}
