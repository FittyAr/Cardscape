namespace Cardscape.Domain.Idempotency;

/// <summary>
/// Identifier of an <see cref="IdempotencyKey"/>. The id is a
/// fresh <see cref="Guid"/> per persisted record; the
/// user-supplied <c>Key</c> string is the dedup key the
/// middleware short-circuits against.
/// </summary>
public sealed record IdempotencyKeyId(Guid Value) : Common.GuidId<IdempotencyKeyId>(Value);
