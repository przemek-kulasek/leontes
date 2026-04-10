using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Leontes.Application;

public interface IApplicationDbContext
{
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
