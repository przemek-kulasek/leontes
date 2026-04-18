namespace Leontes.Domain.Entities;

public sealed class MetricsSummary : Entity
{
    public required DateTime PeriodStart { get; init; }
    public required DateTime PeriodEnd { get; init; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int DegradedRequests { get; set; }
    public int FailedRequests { get; set; }
    public double MedianLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public double MemoryHitRate { get; set; }
    public double ToolSuccessRate { get; set; }
    public int SentinelEventsProcessed { get; set; }
    public int SentinelEventsDropped { get; set; }
}
