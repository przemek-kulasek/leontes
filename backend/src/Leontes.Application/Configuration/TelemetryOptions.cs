namespace Leontes.Application.Configuration;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool Enabled { get; set; } = true;
    public int TraceRetentionDays { get; set; } = 30;
    public int MetricsAggregationIntervalMinutes { get; set; } = 60;
    public ConfidenceThresholdOptions ConfidenceThresholds { get; set; } = new();

    public IReadOnlyList<string> SensitiveFieldPatterns { get; set; } = new[]
    {
        "password", "secret", "token", "key", "credential",
        "iban", "credit_card", "ssn"
    };
}

public sealed class ConfidenceThresholdOptions
{
    public double HighConfidence { get; set; } = 0.8;
    public double LowConfidence { get; set; } = 0.5;
}
