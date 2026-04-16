using Leontes.Domain.Enums;

namespace Leontes.Domain.Entities;

public sealed class StoredProactiveEvent : Entity
{
    public required string EventType { get; set; }
    public required string PayloadJson { get; set; }
    public ProactiveUrgency Urgency { get; set; }
    public ProactiveEventStatus Status { get; set; }
    public string? RequestId { get; set; }
    public string? Response { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}
