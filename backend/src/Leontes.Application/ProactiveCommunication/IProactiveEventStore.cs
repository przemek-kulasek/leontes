using Leontes.Domain.Entities;
using Microsoft.Agents.AI.Workflows;

namespace Leontes.Application.ProactiveCommunication;

public interface IProactiveEventStore
{
    Task StoreAsync(WorkflowEvent evt, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoredProactiveEvent>> GetPendingAsync(CancellationToken cancellationToken);
    Task MarkDeliveredAsync(Guid eventId, CancellationToken cancellationToken);
}
