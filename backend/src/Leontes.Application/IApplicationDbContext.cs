using Leontes.Domain.Entities;

namespace Leontes.Application;

public interface IApplicationDbContext
{
    IQueryable<Conversation> Conversations { get; }
    IQueryable<Message> Messages { get; }
    IQueryable<StoredProactiveEvent> StoredProactiveEvents { get; }
    IQueryable<MemoryEntry> MemoryEntries { get; }
    IQueryable<SynapseEntity> SynapseEntities { get; }
    IQueryable<SynapseRelationship> SynapseRelationships { get; }
    IQueryable<PipelineTrace> PipelineTraces { get; }
    IQueryable<StageTrace> StageTraces { get; }
    IQueryable<DecisionRecord> DecisionRecords { get; }
    IQueryable<MetricsSummary> MetricsSummaries { get; }
    IQueryable<TokenUsageRecord> TokenUsageRecords { get; }
    IQueryable<BudgetPolicy> BudgetPolicies { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
