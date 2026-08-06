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

    // BETA-4-#5 — see test-results/BETA-TEST-REPORT.md.
    //
    // The base RepositoryBase<T,TId>.AddAsync only stages the
    // entity with Set.AddAsync(aggregate) and never calls
    // SaveChangesAsync. The IdempotencyMiddleware calls
    // store.AddAsync on the captured response and then drops
    // the scoped DbContext at the end of the request, so the
    // staged-but-unsaved INSERT is silently lost — the next
    // request from the same user with the same key finds no
    // existing row, treats the call as a fresh miss, and the
    // endpoint executes again. The override commits in-line
    // because the middleware holds the only reference to the
    // DbContext and there's no ambient unit of work to
    // piggy-back on.
    public new async Task AddAsync(IdempotencyKey record, CancellationToken ct = default)
    {
        await Set.AddAsync(record, ct);
        await Db.SaveChangesAsync(ct);
    }
}
