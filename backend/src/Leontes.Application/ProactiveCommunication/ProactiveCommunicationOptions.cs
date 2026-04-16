namespace Leontes.Application.ProactiveCommunication;

public sealed class ProactiveCommunicationOptions
{
    public const string SectionName = "ProactiveCommunication";

    public int DefaultQuestionTimeoutMinutes { get; set; } = 5;
    public int DefaultPermissionTimeoutMinutes { get; set; } = 10;
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int MaxPendingEvents { get; set; } = 100;
    public List<string> ChannelPriority { get; set; } = ["Cli", "Signal", "Telegram"];
}
