namespace Cardscape.Infrastructure.Persistence.Outbox;

public sealed class DomainEventOutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public string BroadcasterType { get; private set; } = string.Empty;
    public long OccurredAtUtcTicks { get; private set; }
    public long CreatedAtUtcTicks { get; private set; }
    public int Attempts { get; private set; }
    public long NextAttemptAtUtcTicks { get; private set; }
    public Guid? LockId { get; private set; }
    public long? LockedUntilUtcTicks { get; private set; }
    public long? ProcessedAtUtcTicks { get; private set; }
    public string? LastError { get; private set; }
    public uint RowVersion { get; private set; }

    public DateTimeOffset OccurredAt => FromUtcTicks(OccurredAtUtcTicks);
    public DateTimeOffset CreatedAt => FromUtcTicks(CreatedAtUtcTicks);
    public DateTimeOffset NextAttemptAt => FromUtcTicks(NextAttemptAtUtcTicks);
    public DateTimeOffset? LockedUntil => LockedUntilUtcTicks is long value ? FromUtcTicks(value) : null;
    public DateTimeOffset? ProcessedAt => ProcessedAtUtcTicks is long value ? FromUtcTicks(value) : null;

    public static DomainEventOutboxMessage Create(
        string eventType,
        string payloadJson,
        string broadcasterType,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt) => new()
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            PayloadJson = payloadJson,
            BroadcasterType = broadcasterType,
            OccurredAtUtcTicks = occurredAt.UtcTicks,
            CreatedAtUtcTicks = createdAt.UtcTicks,
            NextAttemptAtUtcTicks = createdAt.UtcTicks
        };

    public void Complete(DateTimeOffset at)
    {
        ProcessedAtUtcTicks = at.UtcTicks;
        LockId = null;
        LockedUntilUtcTicks = null;
        LastError = null;
    }

    public void Fail(string error, DateTimeOffset nextAttemptAt)
    {
        Attempts++;
        LastError = error.Length <= 2048 ? error : error[..2048];
        NextAttemptAtUtcTicks = nextAttemptAt.UtcTicks;
        LockId = null;
        LockedUntilUtcTicks = null;
    }

    private static DateTimeOffset FromUtcTicks(long ticks) =>
        new(ticks, TimeSpan.Zero);
}
