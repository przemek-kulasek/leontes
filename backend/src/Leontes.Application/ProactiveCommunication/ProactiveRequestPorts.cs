using Leontes.Application.ProactiveCommunication.Requests;
using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication;

public static class ProactiveRequestPorts
{
    public static readonly RequestPort<QuestionRequest, string> Question =
        RequestPort.Create<QuestionRequest, string>("HumanQuestion");

    public static readonly RequestPort<PermissionRequest, bool> Permission =
        RequestPort.Create<PermissionRequest, bool>("HumanPermission");

    public static readonly RequestPort<SentinelAlert, string?> Sentinel =
        RequestPort.Create<SentinelAlert, string?>("SentinelAlert");

    public static readonly RequestPort<ToolApprovalRequest, bool> ToolApproval =
        RequestPort.Create<ToolApprovalRequest, bool>("ToolApproval");
}
