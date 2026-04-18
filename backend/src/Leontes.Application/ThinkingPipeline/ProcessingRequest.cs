using Leontes.Domain.Enums;

namespace Leontes.Application.ThinkingPipeline;

public sealed record ProcessingRequest(
    Guid CorrelationId,
    Guid ConversationId,
    Guid MessageId,
    string Content,
    MessageChannel Channel,
    ProcessingRequestSource Source,
    DateTime EnqueuedAt);

public enum ProcessingRequestSource
{
    User = 0,
    Sentinel = 1
}
