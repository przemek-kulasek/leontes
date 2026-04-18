namespace Leontes.Application.Sentinel;

public interface ISentinelFilter
{
    string MonitorSource { get; }

    SentinelEvent? Evaluate(string rawEvent, IReadOnlyDictionary<string, string> metadata);
}
