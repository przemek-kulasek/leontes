using Leontes.Domain.Enums;

namespace Leontes.Application.Messaging;

public sealed record IncomingMessage(string Sender, string Content, long Timestamp, MessageChannel Channel);
