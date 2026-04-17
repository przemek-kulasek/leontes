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

    IQueryable<Conversation> IApplicationDbContext.Conversations => ConversationSet;
    IQueryable<Message> IApplicationDbContext.Messages => MessageSet;
    IQueryable<StoredProactiveEvent> IApplicationDbContext.StoredProactiveEvents => StoredProactiveEventSet;
    IQueryable<MemoryEntry> IApplicationDbContext.MemoryEntries => MemoryEntrySet;
    IQueryable<SynapseEntity> IApplicationDbContext.SynapseEntities => SynapseEntitySet;
    IQueryable<SynapseRelationship> IApplicationDbContext.SynapseRelationships => SynapseRelationshipSet;

    void IApplicationDbContext.Add<TEntity>(TEntity entity) => base.Add(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
