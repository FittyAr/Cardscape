using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IIdempotencyKeyStore"/>.
/// The (OwnerId, Key) pair is unique so two retries of the
/// same logical request from the same user collapse to a
/// single row.
/// </summary>
public sealed class IdempotencyKeyRepository(CardscapeDbContext db)
    : RepositoryBase<IdempotencyKey, IdempotencyKeyId>(db), IIdempotencyKeyStore
{
    public async Task<IdempotencyKey?> FindAsync(
        UserId ownerId,
        IdempotencyKeyValue key,
        CancellationToken ct = default)
    {
        return await Set
            .AsNoTracking()
            .FirstOrDefaultAsync(
                k => k.OwnerId == ownerId
                  && k.Key == key,
                ct);
    }
}
