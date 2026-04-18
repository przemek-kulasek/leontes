using Leontes.Application;
using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Leontes.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Conversation> ConversationSet => Set<Conversation>();
    public DbSet<Message> MessageSet => Set<Message>();
    public DbSet<StoredProactiveEvent> StoredProactiveEventSet => Set<StoredProactiveEvent>();
    public DbSet<MemoryEntry> MemoryEntrySet => Set<MemoryEntry>();
    public DbSet<SynapseEntity> SynapseEntitySet => Set<SynapseEntity>();
    public DbSet<SynapseRelationship> SynapseRelationshipSet => Set<SynapseRelationship>();
    public DbSet<PipelineTrace> PipelineTraceSet => Set<PipelineTrace>();
    public DbSet<StageTrace> StageTraceSet => Set<StageTrace>();
    public DbSet<DecisionRecord> DecisionRecordSet => Set<DecisionRecord>();
    public DbSet<MetricsSummary> MetricsSummarySet => Set<MetricsSummary>();

    IQueryable<Conversation> IApplicationDbContext.Conversations => ConversationSet;
    IQueryable<Message> IApplicationDbContext.Messages => MessageSet;
    IQueryable<StoredProactiveEvent> IApplicationDbContext.StoredProactiveEvents => StoredProactiveEventSet;
    IQueryable<MemoryEntry> IApplicationDbContext.MemoryEntries => MemoryEntrySet;
    IQueryable<SynapseEntity> IApplicationDbContext.SynapseEntities => SynapseEntitySet;
    IQueryable<SynapseRelationship> IApplicationDbContext.SynapseRelationships => SynapseRelationshipSet;
    IQueryable<PipelineTrace> IApplicationDbContext.PipelineTraces => PipelineTraceSet;
    IQueryable<StageTrace> IApplicationDbContext.StageTraces => StageTraceSet;
    IQueryable<DecisionRecord> IApplicationDbContext.DecisionRecords => DecisionRecordSet;
    IQueryable<MetricsSummary> IApplicationDbContext.MetricsSummaries => MetricsSummarySet;

    void IApplicationDbContext.Add<TEntity>(TEntity entity) => base.Add(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
