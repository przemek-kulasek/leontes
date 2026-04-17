using System.Collections.Concurrent;
using Leontes.Application.Configuration;
using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Application.ProactiveCommunication.Requests;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.ProactiveCommunication;

/// <summary>
/// Schedules a per-type timeout for every outstanding <see cref="ExternalRequest"/>.
/// On expiry applies the default behavior defined in feature 85 and emits a
/// <see cref="TimeoutEvent"/> so the user is notified via the SSE bridge.
/// </summary>
public sealed class RequestPortTimeoutScheduler(
    IWorkflowSessionManager sessions,
    IWorkflowEventBridge eventBridge,
    IOptions<ResilienceOptions> options,
    ILogger<RequestPortTimeoutScheduler> logger) : IRequestPortTimeoutScheduler, IDisposable
{
    private readonly RequestPortOptions _options = options.Value.RequestPort;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();

    public void Schedule(ExternalRequest request)
    {
        var (timeout, requestType) = Classify(request);
        if (timeout <= TimeSpan.Zero)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        if (!_pending.TryAdd(request.RequestId, cts))
        {
            cts.Dispose();
            return;
        }

        _ = Task.Run(() => RunAsync(request, requestType, timeout, cts.Token), CancellationToken.None);
    }

    public void Cancel(string requestId)
    {
        if (_pending.TryRemove(requestId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task RunAsync(
        ExternalRequest request,
        string requestType,
        TimeSpan timeout,
        CancellationToken ct)
    {
        try
        {
            await Task.Delay(timeout, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Take ownership — if another responder took it first, bail
        var taken = sessions.TakePendingRequest(request.RequestId);
        if (taken is null)
        {
            _pending.TryRemove(request.RequestId, out _);
            return;
        }

        var run = sessions.GetActiveRun();
        if (run is null)
        {
            logger.LogWarning(
                "Request {RequestId} timed out but no active workflow run is registered",
                request.RequestId);
            _pending.TryRemove(request.RequestId, out _);
            return;
        }

        try
        {
            var (response, appliedDefault) = DefaultResponseFor(request, requestType);
            await run.SendResponseAsync(taken.CreateResponse(response));

            await eventBridge.PublishEventAsync(
                new TimeoutEvent(request.RequestId, requestType, appliedDefault),
                CancellationToken.None);

            logger.LogInformation(
                "Request {RequestId} ({RequestType}) timed out after {Timeout}; default={Default}",
                request.RequestId, requestType, timeout, appliedDefault);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to apply timeout default for request {RequestId}",
                request.RequestId);
        }
        finally
        {
            _pending.TryRemove(request.RequestId, out _);
        }
    }

    private (TimeSpan Timeout, string RequestType) Classify(ExternalRequest request)
    {
        if (request.TryGetDataAs<QuestionRequest>(out _))
            return (TimeSpan.FromMinutes(_options.QuestionTimeoutMinutes), nameof(QuestionRequest));
        if (request.TryGetDataAs<ToolApprovalRequest>(out _))
            return (TimeSpan.FromMinutes(_options.ToolApprovalTimeoutMinutes), nameof(ToolApprovalRequest));
        if (request.TryGetDataAs<SentinelAlert>(out _))
            return (TimeSpan.FromMinutes(_options.SentinelAlertTimeoutMinutes), nameof(SentinelAlert));
        if (request.TryGetDataAs<PermissionRequest>(out _))
            return (TimeSpan.FromMinutes(_options.PermissionTimeoutMinutes), nameof(PermissionRequest));

        return (TimeSpan.Zero, request.PortInfo.PortId);
    }

    private static (object Response, string Default) DefaultResponseFor(
        ExternalRequest request, string requestType)
    {
        // Defaults per feature 85 error handling table
        return requestType switch
        {
            nameof(QuestionRequest) => ("", "proceed-best-guess"),
            nameof(ToolApprovalRequest) => (false, "deny"),
            nameof(PermissionRequest) => (false, "deny"),
            nameof(SentinelAlert) => ((object?)null!, "dismiss"),
            _ => (null!, "none")
        };
    }

    public void Dispose()
    {
        foreach (var cts in _pending.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _pending.Clear();
    }
}
