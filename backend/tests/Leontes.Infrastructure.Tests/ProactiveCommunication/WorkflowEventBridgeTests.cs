using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Domain.Enums;
using Leontes.Infrastructure.ProactiveCommunication;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leontes.Infrastructure.Tests.ProactiveCommunication;

public class WorkflowEventBridgeTests
{
    private readonly WorkflowEventBridge _bridge = new(NullLogger<WorkflowEventBridge>.Instance);

    [Fact]
    public void HasActiveClients_NoClients_ReturnsFalse()
    {
        Assert.False(_bridge.HasActiveClients);
    }

    [Fact]
    public void HasActiveClients_AfterRegister_ReturnsTrue()
    {
        _bridge.RegisterClient("client-1");

        Assert.True(_bridge.HasActiveClients);
    }

    [Fact]
    public void HasActiveClients_AfterUnregister_ReturnsFalse()
    {
        _bridge.RegisterClient("client-1");
        _bridge.UnregisterClient("client-1");

        Assert.False(_bridge.HasActiveClients);
    }

    [Fact]
    public async Task PublishEventAsync_DeliversToRegisteredClient()
    {
        _bridge.RegisterClient("client-1");
        var notification = new NotificationEvent("Test", "Content", ProactiveUrgency.Medium);

        await _bridge.PublishEventAsync(notification, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var events = new List<WorkflowEvent>();
        await foreach (var evt in _bridge.ReadEventsAsync("client-1", cts.Token))
        {
            events.Add(evt);
            break;
        }

        Assert.Single(events);
        Assert.IsType<NotificationEvent>(events[0]);
    }

    [Fact]
    public async Task PublishEventAsync_DeliversToMultipleClients()
    {
        _bridge.RegisterClient("client-1");
        _bridge.RegisterClient("client-2");
        var notification = new NotificationEvent("Test", "Content", ProactiveUrgency.Low);

        await _bridge.PublishEventAsync(notification, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var events1 = new List<WorkflowEvent>();
        await foreach (var evt in _bridge.ReadEventsAsync("client-1", cts.Token))
        {
            events1.Add(evt);
            break;
        }

        var events2 = new List<WorkflowEvent>();
        await foreach (var evt in _bridge.ReadEventsAsync("client-2", cts.Token))
        {
            events2.Add(evt);
            break;
        }

        Assert.Single(events1);
        Assert.Single(events2);
    }

    [Fact]
    public async Task ReadEventsAsync_UnknownClient_YieldsNothing()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var events = new List<WorkflowEvent>();
        await foreach (var evt in _bridge.ReadEventsAsync("unknown", cts.Token))
        {
            events.Add(evt);
        }

        Assert.Empty(events);
    }

    [Fact]
    public async Task UnregisterClient_CompletesReadStream()
    {
        _bridge.RegisterClient("client-1");

        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var readToken = readCts.Token;

        var readTask = Task.Run(async () =>
        {
            var events = new List<WorkflowEvent>();
            await foreach (var evt in _bridge.ReadEventsAsync("client-1", readToken))
            {
                events.Add(evt);
            }
            return events;
        }, readToken);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        _bridge.UnregisterClient("client-1");

        var result = await readTask.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Empty(result);
    }

    [Fact]
    public async Task PublishEventAsync_PreservesEventPayload()
    {
        _bridge.RegisterClient("client-1");
        var progress = new ProgressEvent("Enrich", "Searching memory...", 0.4);

        await _bridge.PublishEventAsync(progress, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var evt in _bridge.ReadEventsAsync("client-1", cts.Token))
        {
            var payload = Assert.IsType<ProgressPayload>(evt.Data);
            Assert.Equal("Enrich", payload.Stage);
            Assert.Equal("Searching memory...", payload.Description);
            Assert.Equal(0.4, payload.Progress);
            break;
        }
    }
}
