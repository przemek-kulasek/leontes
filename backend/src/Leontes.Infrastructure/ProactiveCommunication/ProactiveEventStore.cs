using System.Text.Json;
using Leontes.Application;
using Leontes.Application.ProactiveCommunication;
using Leontes.Application.ProactiveCommunication.Events;
using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Leontes.Infrastructure.ProactiveCommunication;

public sealed class ProactiveEventStore(
    IApplicationDbContext db,
    IOptions<ProactiveCommunicationOptions> options) : IProactiveEventStore
{
    public async Task StoreAsync(WorkflowEvent evt, CancellationToken cancellationToken)
    {
        var (eventType, urgency, requestId) = ExtractMetadata(evt);

        var stored = new StoredProactiveEvent
        {
            EventType = eventType,
            PayloadJson = JsonSerializer.Serialize(evt.Data),
            Urgency = urgency,
            Status = ProactiveEventStatus.Pending,
            RequestId = requestId
        };

        db.Add(stored);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredProactiveEvent>> GetPendingAsync(
        CancellationToken cancellationToken)
    {
        return await db.StoredProactiveEvents
            .AsNoTracking()
            .Where(e => e.Status == ProactiveEventStatus.Pending)
            .OrderBy(e => e.Created)
            .Take(options.Value.MaxPendingEvents)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkDeliveredAsync(Guid eventId, CancellationToken cancellationToken)
    {
        // Status check in the WHERE clause prevents duplicate delivery
        // when multiple clients flush pending events concurrently
        await db.StoredProactiveEvents
            .Where(e => e.Id == eventId && e.Status == ProactiveEventStatus.Pending)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.Status, ProactiveEventStatus.Delivered)
                    .SetProperty(e => e.DeliveredAt, DateTime.UtcNow),
                cancellationToken);
    }

    private static (string EventType, ProactiveUrgency Urgency, string? RequestId) ExtractMetadata(
        WorkflowEvent evt)
    {
        return evt switch
        {
            NotificationEvent n when n.Data is NotificationPayload p =>
                ("Notification", p.Urgency, null),
            RequestInfoEvent r =>
                ("Request", ProactiveUrgency.High, r.Request.RequestId),
            ProgressEvent =>
                ("Progress", ProactiveUrgency.Low, null),
            InsightEvent =>
                ("Insight", ProactiveUrgency.Low, null),
            _ =>
                (evt.GetType().Name, ProactiveUrgency.Low, null)
        };
    }
}
