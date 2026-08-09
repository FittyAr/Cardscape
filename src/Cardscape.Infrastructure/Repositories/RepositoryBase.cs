using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Common;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

/// <summary>Generic EF Core implementation of <see cref="IRepository{T, TId}"/>.</summary>
public abstract class RepositoryBase<TEntity, TId>(DbContext db) : IRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    protected DbContext Db { get; } = db;
    protected DbSet<TEntity> Set => Db.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default) =>
        await Set.FindAsync(new object?[] { id }, ct);

    public virtual async Task AddAsync(TEntity aggregate, CancellationToken ct = default) =>
        await Set.AddAsync(aggregate, ct);

    public virtual void Remove(TEntity aggregate) => Set.Remove(aggregate);
}
