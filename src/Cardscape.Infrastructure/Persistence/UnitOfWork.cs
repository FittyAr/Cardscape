using Cardscape.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Persistence;

/// <summary>
/// Unit of Work implemented on top of <see cref="CardscapeDbContext"/>.
/// </summary>
public sealed class UnitOfWork(CardscapeDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
