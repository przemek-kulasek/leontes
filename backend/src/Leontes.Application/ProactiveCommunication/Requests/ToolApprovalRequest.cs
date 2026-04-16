namespace Leontes.Application.ProactiveCommunication.Requests;

public sealed record ToolApprovalRequest(
    string ToolName,
    string Description,
    string ToolCall);
