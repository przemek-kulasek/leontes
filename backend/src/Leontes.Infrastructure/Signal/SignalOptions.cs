namespace Leontes.Infrastructure.Signal;

public sealed class SignalOptions
{
    public const string SectionName = "Signal";

    public string BaseUrl { get; set; } = "http://localhost:8081";
    public string PhoneNumber { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 2;
    public List<string> AllowedSenders { get; set; } = [];
}
