using Leontes.Domain.Entities;

namespace Leontes.Application;

public interface IApplicationDbContext
{
    IQueryable<Conversation> Conversations { get; }
    IQueryable<Message> Messages { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
