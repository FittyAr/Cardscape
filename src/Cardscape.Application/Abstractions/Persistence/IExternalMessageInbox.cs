namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Durable idempotency boundary for externally delivered messages.</summary>
public interface IExternalMessageInbox
{
    Task<ExternalMessageReservation> BeginAsync(
        string source,
        string messageHash,
        DateTimeOffset now,
        CancellationToken ct = default);

    Task<bool> CompleteAsync(
        Guid receiptId,
        Guid resourceId,
        DateTimeOffset completedAt,
        CancellationToken ct = default);

    Task ReleaseAsync(Guid receiptId, CancellationToken ct = default);
}

public enum ExternalMessageReservationState
{
    Acquired,
    InProgress,
    Completed
}

public sealed record ExternalMessageReservation(
    Guid ReceiptId,
    ExternalMessageReservationState State,
    Guid? ResourceId = null);
