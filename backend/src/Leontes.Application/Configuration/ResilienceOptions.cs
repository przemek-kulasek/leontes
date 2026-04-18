namespace Leontes.Application.Configuration;

public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    public int QueueCapacity { get; set; } = 100;
    public int EnqueueWaitSeconds { get; set; } = 5;

    public LlmResilienceOptions Llm { get; set; } = new();
    public RequestPortOptions RequestPort { get; set; } = new();
    public ContextWindowOptions ContextWindow { get; set; } = new();
    public ChannelDeliveryOptions ChannelDelivery { get; set; } = new();
    public SentinelResilienceOptions Sentinel { get; set; } = new();
    public DegradedModeOptions DegradedMode { get; set; } = new();
}

public sealed class LlmResilienceOptions
{
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxRetries { get; set; } = 3;
    public int RetryBaseDelaySeconds { get; set; } = 1;
    public int ConsecutiveFailuresBeforeDegraded { get; set; } = 3;
    public int DegradedWindowMinutes { get; set; } = 5;
}

public sealed class RequestPortOptions
{
    public int QuestionTimeoutMinutes { get; set; } = 10;
    public int ToolApprovalTimeoutMinutes { get; set; } = 30;
    public int SentinelAlertTimeoutMinutes { get; set; } = 5;
    public int PermissionTimeoutMinutes { get; set; } = 10;
}

public sealed class ContextWindowOptions
{
    public int BufferPercentage { get; set; } = 10;
    public int SummaryTriggerTurns { get; set; } = 20;
    public int MinRecentTurns { get; set; } = 10;
    public int DefaultModelTokenLimit { get; set; } = 8192;
    public int AverageCharsPerToken { get; set; } = 4;
}

public sealed class ChannelDeliveryOptions
{
    public int MaxRetries { get; set; } = 2;
    public int RetryDelaySeconds { get; set; } = 5;
}

public sealed class SentinelResilienceOptions
{
    public int MaxEventsPerMinutePerMonitor { get; set; } = 1;
    public int QueueDepthLimit { get; set; } = 50;
}

public sealed class DegradedModeOptions
{
    public int LlmPollIntervalSeconds { get; set; } = 30;
}
