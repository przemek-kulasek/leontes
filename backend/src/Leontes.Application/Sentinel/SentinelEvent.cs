namespace Leontes.Application.Sentinel;

public sealed record SentinelEvent(
    string MonitorSource,
    string EventType,
    string? Pattern,
    string Summary,
    IReadOnlyDictionary<string, string> Metadata,
    DateTime OccurredAt,
    SentinelPriority Priority);
