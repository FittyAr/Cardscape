using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Npgsql;

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

    public async Task<bool> TryReserveAsync(
        IdempotencyKey reservation,
        CancellationToken ct = default)
    {
        await Set.AddAsync(reservation, ct);
        try
        {
            await Db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            Db.Entry(reservation).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> CompleteReservationAsync(
        IdempotencyKeyId id,
        int responseStatusCode,
        string responseJson,
        DateTimeOffset completedAt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(responseJson);
        if (responseStatusCode is < 100 or > 599
            || responseStatusCode == IdempotencyKey.ReservationStatusCode)
        {
            throw new ArgumentOutOfRangeException(nameof(responseStatusCode));
        }

        int affected = await Set
            .Where(record => record.Id == id
                && record.ResponseStatusCode == IdempotencyKey.ReservationStatusCode)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.ResponseStatusCode, responseStatusCode)
                .SetProperty(record => record.ResponseJson, responseJson)
                .SetProperty(record => record.ExpiresAt,
                    completedAt + IdempotencyKey.RetentionWindow)
                .SetProperty(record => record.UpdatedAt, completedAt)
                .SetProperty(record => record.RowVersion, record => record.RowVersion + 1), ct);
        return affected == 1;
    }

    public async Task ReleaseAsync(IdempotencyKeyId id, CancellationToken ct = default) =>
        _ = await Set.Where(record => record.Id == id).ExecuteDeleteAsync(ct);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqliteException sqlite)
        {
            return sqlite.SqliteErrorCode == 19 && sqlite.SqliteExtendedErrorCode == 2067;
        }

        if (exception.InnerException is PostgresException postgres)
        {
            return postgres.SqlState == PostgresErrorCodes.UniqueViolation;
        }

        if (exception.InnerException is MySqlException mysql)
        {
            return mysql.Number == 1062;
        }

        string message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase);
    }
}
