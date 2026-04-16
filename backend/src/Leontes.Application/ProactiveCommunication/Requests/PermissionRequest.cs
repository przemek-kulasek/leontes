namespace Leontes.Application.ProactiveCommunication.Requests;

public sealed record PermissionRequest(
    string Action,
    string Details,
    IReadOnlyList<string>? Context);
