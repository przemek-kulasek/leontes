using System.Text.Json;
using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ProactiveCommunication.Events;
using Microsoft.Agents.AI.Workflows;
using NotificationEvent = Leontes.Application.ProactiveCommunication.Events.NotificationEvent;
using Microsoft.Extensions.Options;

namespace Leontes.Api.Endpoints;

public static class StreamEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static RouteGroupBuilder MapStreamEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/stream", HandleStream)
            .WithName("Stream")
            .WithSummary("Persistent SSE channel for workflow events")
            .WithTags("Stream")
            .Produces(200, contentType: "text/event-stream")
            .ExcludeFromDescription();

        group.MapPost("/stream/respond", HandleRespond)
            .WithName("StreamRespond")
            .WithSummary("Respond to a workflow request")
            .WithTags("Stream")
            .Accepts<RespondRequest>("application/json")
            .Produces(200)
            .Produces(404);

        return group;
    }

    private static async Task HandleStream(
        HttpContext httpContext,
        IWorkflowEventBridge bridge,
        IWorkflowSessionManager sessions,
        IRequestPortTimeoutScheduler timeouts,
        IServiceScopeFactory scopeFactory,
        IOptions<ProactiveCommunicationOptions> options,
        ILogger<IWorkflowEventBridge> logger,
        CancellationToken cancellationToken)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        var clientId = httpContext.Connection.Id;
        bridge.RegisterClient(clientId);

        try
        {
            // Flush pending events in a dedicated scope so the DbContext
            // is disposed before the long-lived SSE loop starts
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var eventStore = scope.ServiceProvider.GetRequiredService<IProactiveEventStore>();
                var pending = await eventStore.GetPendingAsync(cancellationToken);
                foreach (var stored in pending)
                {
                    var sseEvent = FormatStoredEvent(stored);
                    if (sseEvent is not null)
                    {
                        await httpContext.Response.WriteAsync(sseEvent, cancellationToken);
                        await httpContext.Response.Body.FlushAsync(cancellationToken);
                        await eventStore.MarkDeliveredAsync(stored.Id, cancellationToken);
                    }
                }
            }

            var heartbeatInterval = TimeSpan.FromSeconds(
                options.Value.HeartbeatIntervalSeconds);

            using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var heartbeatTask = RunHeartbeatAsync(
                httpContext, heartbeatInterval, heartbeatCts.Token);

            try
            {
                await foreach (var evt in bridge.ReadEventsAsync(clientId, cancellationToken))
                {
                    if (evt is RequestInfoEvent req)
                    {
                        sessions.TrackPendingRequest(req.Request);
                        timeouts.Schedule(req.Request);
                    }

                    var sseEvent = FormatWorkflowEvent(evt);
                    if (sseEvent is not null)
                    {
                        await httpContext.Response.WriteAsync(sseEvent, cancellationToken);
                        await httpContext.Response.Body.FlushAsync(cancellationToken);
                    }
                }
            }
            finally
            {
                await heartbeatCts.CancelAsync();
                try { await heartbeatTask; }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            bridge.UnregisterClient(clientId);
        }
    }

    private static async Task HandleRespond(
        RespondRequest request,
        IWorkflowSessionManager sessions,
        IRequestPortTimeoutScheduler timeouts)
    {
        var run = sessions.GetActiveRun();
        if (run is null)
        {
            throw new Domain.Exceptions.NotFoundException("No active workflow run");
        }

        var pending = sessions.TakePendingRequest(request.RequestId);
        if (pending is null)
        {
            throw new Domain.Exceptions.NotFoundException(
                $"Request '{request.RequestId}' not found or already responded");
        }

        timeouts.Cancel(request.RequestId);

        await run.SendResponseAsync(pending.CreateResponse(request.Response));
    }

    private static async Task RunHeartbeatAsync(
        HttpContext httpContext,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);
            await httpContext.Response.WriteAsync(
                "event: heartbeat\ndata: {}\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static string? FormatWorkflowEvent(WorkflowEvent evt)
    {
        return evt switch
        {
            RequestInfoEvent req => FormatSseEvent("request", new
            {
                requestId = req.Request.RequestId,
                portId = req.Request.PortInfo.PortId,
                data = req.Request.TryGetDataAs<object>(out var d) ? d : null
            }),
            NotificationEvent n when n.Data is NotificationPayload p => FormatSseEvent(
                "notification", p),
            ProgressEvent p when p.Data is ProgressPayload pp => FormatSseEvent(
                "progress", pp),
            InsightEvent i when i.Data is InsightPayload ip => FormatSseEvent(
                "insight", ip),
            TokenStreamEvent t => FormatSseEvent("token", new
            {
                text = t.Text
            }),
            TimeoutEvent to when to.Data is TimeoutPayload tp => FormatSseEvent(
                "timeout", tp),
            SuperStepCompletedEvent s => FormatSseEvent("checkpoint", new
            {
                stepNumber = s.Data
            }),
            _ => null
        };
    }

    private static string? FormatStoredEvent(
        Domain.Entities.StoredProactiveEvent stored)
    {
        return FormatSseEvent(stored.EventType.ToLowerInvariant(), new
        {
            storedEventId = stored.Id,
            requestId = stored.RequestId,
            payload = JsonSerializer.Deserialize<JsonElement>(stored.PayloadJson),
            urgency = stored.Urgency.ToString()
        });
    }

    private static string FormatSseEvent(string eventType, object data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        return $"event: {eventType}\ndata: {json}\n\n";
    }
}
