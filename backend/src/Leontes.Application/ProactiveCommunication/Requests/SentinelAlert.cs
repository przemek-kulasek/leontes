namespace Leontes.Application.ProactiveCommunication.Requests;

public sealed record SentinelAlert(
    string MonitorSource,
    string Summary,
    IReadOnlyList<string>? SuggestedActions);
