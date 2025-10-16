#pragma warning disable CA1716 // Identifier should not match keyword
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Persistence;

public interface IAppDbContext
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
#pragma warning restore CA1716
