using Cardscape.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Persistence.Inbox;

internal sealed class ExternalMessageInbox(CardscapeDbContext db) : IExternalMessageInbox
{
    public async Task<ExternalMessageReservation> BeginAsync(
        string source,
        string messageHash,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageHash);

        while (true)
        {
            ExternalMessageReceipt? existing = await db.Set<ExternalMessageReceipt>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    receipt => receipt.Source == source && receipt.MessageHash == messageHash,
                    ct);
            if (existing is not null)
            {
                if (existing.CompletedAt is not null)
                {
                    return new ExternalMessageReservation(
                        existing.Id,
                        ExternalMessageReservationState.Completed,
                        existing.ResourceId);
                }

                if (existing.LeaseExpiresAtUtcTicks > now.UtcTicks)
                {
                    return new ExternalMessageReservation(
                        existing.Id,
                        ExternalMessageReservationState.InProgress);
                }

                _ = await db.Set<ExternalMessageReceipt>()
                    .Where(receipt => receipt.Id == existing.Id
                        && receipt.CompletedAt == null
                        && receipt.LeaseExpiresAtUtcTicks <= now.UtcTicks)
                    .ExecuteDeleteAsync(ct);
                continue;
            }

            var receipt = new ExternalMessageReceipt(Guid.NewGuid(), source, messageHash, now);
            await db.Set<ExternalMessageReceipt>().AddAsync(receipt, ct);
            try
            {
                await db.SaveChangesAsync(ct);
                return new ExternalMessageReservation(
                    receipt.Id,
                    ExternalMessageReservationState.Acquired);
            }
            catch (DbUpdateException ex) when (DatabaseExceptionClassifier.IsUniqueConstraintViolation(ex))
            {
                db.Entry(receipt).State = EntityState.Detached;
            }
        }
    }

    public async Task<bool> CompleteAsync(
        Guid receiptId,
        Guid resourceId,
        DateTimeOffset completedAt,
        CancellationToken ct = default)
    {
        int affected = await db.Set<ExternalMessageReceipt>()
            .Where(receipt => receipt.Id == receiptId && receipt.CompletedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(receipt => receipt.ResourceId, resourceId)
                .SetProperty(receipt => receipt.CompletedAt, completedAt)
                .SetProperty(receipt => receipt.LeaseExpiresAtUtcTicks, completedAt.UtcTicks), ct);
        return affected == 1;
    }

    public async Task ReleaseAsync(Guid receiptId, CancellationToken ct = default) =>
        _ = await db.Set<ExternalMessageReceipt>()
            .Where(receipt => receipt.Id == receiptId && receipt.CompletedAt == null)
            .ExecuteDeleteAsync(ct);
}
