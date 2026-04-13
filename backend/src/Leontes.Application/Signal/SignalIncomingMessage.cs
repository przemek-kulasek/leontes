namespace Leontes.Application.Signal;

public sealed record SignalIncomingMessage(string Sender, string Content, long Timestamp);
