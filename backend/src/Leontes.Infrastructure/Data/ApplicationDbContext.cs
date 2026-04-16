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

    IQueryable<Conversation> IApplicationDbContext.Conversations => ConversationSet;
    IQueryable<Message> IApplicationDbContext.Messages => MessageSet;
    IQueryable<StoredProactiveEvent> IApplicationDbContext.StoredProactiveEvents => StoredProactiveEventSet;

    void IApplicationDbContext.Add<TEntity>(TEntity entity) => base.Add(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
