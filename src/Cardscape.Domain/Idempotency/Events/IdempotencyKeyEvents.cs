using Cardscape.Domain.Common;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Idempotency.Events;

/// <summary>
/// Raised when a new <see cref="IdempotencyKey"/> is recorded
/// by the middleware. Carries the owner and the request
/// payload hash so downstream consumers (audit, analytics) can
/// reconstruct what was sent.
/// </summary>
public sealed record IdempotencyKeyRecorded(
    IdempotencyKeyId KeyId,
    IdempotencyKeyValue Key,
    UserId OwnerId,
    string RequestHash,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
