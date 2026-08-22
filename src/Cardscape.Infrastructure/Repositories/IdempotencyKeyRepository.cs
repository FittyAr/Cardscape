using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
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

        return false;
    }
}
