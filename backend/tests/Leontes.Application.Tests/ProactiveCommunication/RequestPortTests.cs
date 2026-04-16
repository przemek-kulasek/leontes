using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ProactiveCommunication.Requests;

namespace Leontes.Application.Tests.ProactiveCommunication;

public class RequestPortTests
{
    [Fact]
    public void QuestionPort_HasCorrectId()
    {
        Assert.Equal("HumanQuestion", ProactiveRequestPorts.Question.Id);
    }

    [Fact]
    public void PermissionPort_HasCorrectId()
    {
        Assert.Equal("HumanPermission", ProactiveRequestPorts.Permission.Id);
    }

    [Fact]
    public void SentinelPort_HasCorrectId()
    {
        Assert.Equal("SentinelAlert", ProactiveRequestPorts.Sentinel.Id);
    }

    [Fact]
    public void ToolApprovalPort_HasCorrectId()
    {
        Assert.Equal("ToolApproval", ProactiveRequestPorts.ToolApproval.Id);
    }

    [Fact]
    public void QuestionPort_HasCorrectTypes()
    {
        Assert.Equal(typeof(QuestionRequest), ProactiveRequestPorts.Question.Request);
        Assert.Equal(typeof(string), ProactiveRequestPorts.Question.Response);
    }

    [Fact]
    public void PermissionPort_HasCorrectTypes()
    {
        Assert.Equal(typeof(PermissionRequest), ProactiveRequestPorts.Permission.Request);
        Assert.Equal(typeof(bool), ProactiveRequestPorts.Permission.Response);
    }
}
