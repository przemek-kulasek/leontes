using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Leontes.Application.ProactiveCommunication;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace Leontes.Infrastructure.ProactiveCommunication;

public sealed class WorkflowEventBridge(ILogger<WorkflowEventBridge> logger) : IWorkflowEventBridge
{
    private readonly Dictionary<string, Channel<WorkflowEvent>> _clients = new();
    private readonly Lock _lock = new();

    public bool HasActiveClients
    {
        get
        {
            lock (_lock)
            {
                return _clients.Count > 0;
            }
        }
    }

    public void RegisterClient(string clientId)
    {
        var channel = Channel.CreateBounded<WorkflowEvent>(
            new BoundedChannelOptions(256)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        lock (_lock)
        {
            if (_clients.Remove(clientId, out var old))
            {
                old.Writer.TryComplete();
            }

            _clients[clientId] = channel;
        }

        logger.LogInformation("Client {ClientId} registered for workflow events", clientId);
    }

    public void UnregisterClient(string clientId)
    {
        lock (_lock)
        {
            if (_clients.Remove(clientId, out var channel))
            {
                channel.Writer.TryComplete();
            }
        }

        logger.LogInformation("Client {ClientId} unregistered from workflow events", clientId);
    }

    public async Task PublishEventAsync(WorkflowEvent evt, CancellationToken cancellationToken)
    {
        List<Channel<WorkflowEvent>> channels;

        lock (_lock)
        {
            channels = [.. _clients.Values];
        }

        foreach (var channel in channels)
        {
            await channel.Writer.WriteAsync(evt, cancellationToken);
        }
    }

    public async IAsyncEnumerable<WorkflowEvent> ReadEventsAsync(
        string clientId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Channel<WorkflowEvent>? channel;

        lock (_lock)
        {
            _clients.TryGetValue(clientId, out channel);
        }

        if (channel is null)
        {
            yield break;
        }

        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }
    }
}
