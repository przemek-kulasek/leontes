namespace Leontes.Application.Sentinel;

public interface ISentinelHeuristicEngine
{
    SentinelEvent? Process(
        string monitorSource,
        string rawEvent,
        IReadOnlyDictionary<string, string> metadata);
}
