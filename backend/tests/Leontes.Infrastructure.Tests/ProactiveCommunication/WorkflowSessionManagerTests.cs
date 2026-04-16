using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ProactiveCommunication.Requests;
using Leontes.Infrastructure.ProactiveCommunication;
using Microsoft.Agents.AI.Workflows;

namespace Leontes.Infrastructure.Tests.ProactiveCommunication;

public class WorkflowSessionManagerTests
{
    private readonly WorkflowSessionManager _manager = new();

    [Fact]
    public void GetActiveRun_NoRunSet_ReturnsNull()
    {
        Assert.Null(_manager.GetActiveRun());
    }

    [Fact]
    public void TrackPendingRequest_TakePendingRequest_ReturnsRequest()
    {
        var port = ProactiveRequestPorts.Question;
        var request = ExternalRequest.Create(
            port,
            new QuestionRequest("Test", "Content", null, null),
            "req-1");

        _manager.TrackPendingRequest(request);

        var taken = _manager.TakePendingRequest("req-1");

        Assert.NotNull(taken);
        Assert.Equal("req-1", taken.RequestId);
    }

    [Fact]
    public void TakePendingRequest_UnknownId_ReturnsNull()
    {
        Assert.Null(_manager.TakePendingRequest("nonexistent"));
    }

    [Fact]
    public void TakePendingRequest_CalledTwice_ReturnsNullSecondTime()
    {
        var port = ProactiveRequestPorts.Permission;
        var request = ExternalRequest.Create(
            port,
            new PermissionRequest("delete-file", "/tmp/test.txt", null),
            "req-2");

        _manager.TrackPendingRequest(request);
        _manager.TakePendingRequest("req-2");

        Assert.Null(_manager.TakePendingRequest("req-2"));
    }

    [Fact]
    public void ClearActiveRun_ClearsPendingRequests()
    {
        var port = ProactiveRequestPorts.Question;
        var request = ExternalRequest.Create(
            port,
            new QuestionRequest("Test", "Content", null, null),
            "req-3");

        _manager.TrackPendingRequest(request);
        _manager.ClearActiveRun();

        Assert.Null(_manager.TakePendingRequest("req-3"));
    }
}
