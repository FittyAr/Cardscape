using Cardscape.Domain.Common;
using Cardscape.Domain.Idempotency.Events;
using Cardscape.Domain.Members;

namespace Cardscape.Domain.Idempotency;

/// <summary>
/// A user-supplied idempotency key, paired with the SHA-256
/// hash of the request body that produced the stored response.
/// The middleware short-circuits any repeat of the same
/// <see cref="Key"/> from the same <see cref="OwnerId"/> to
/// the previously-recorded <see cref="ResponseJson"/>.
/// </summary>
/// <remarks>
/// <para>
/// A different request payload (different hash) is rejected
/// with <c>idempotency.key.payload_mismatch</c> so callers
/// cannot accidentally reuse a key for a different operation.
/// </para>
/// <para>
/// The stored response is opaque JSON; the middleware writes
/// it back verbatim with the same HTTP status code so a
/// retried write is byte-for-byte identical to the first
/// attempt.
/// </para>
/// <para>
/// Each row carries an <see cref="ExpiresAt"/>. The
/// middleware treats a key past its expiry as a miss (the
/// next caller is free to record a fresh response). Without
/// an expiry a user-supplied key like <c>"foo"</c> would
/// replay forever, defeating the at-most-once guarantee
/// for any later write that happened to share the same
/// key. The retention window is 24 hours — generous
/// enough for any reasonable retry window, short enough
/// that the table does not grow without bound.
/// </para>
/// </remarks>
public sealed class IdempotencyKey : AggregateRoot<IdempotencyKeyId>
{
    /// <summary>Internal status used while the first caller owns the key.</summary>
    public const int ReservationStatusCode = 102;

    /// <summary>Maximum time an abandoned execution blocks a retry.</summary>
    public static readonly TimeSpan ReservationWindow = TimeSpan.FromMinutes(15);

    /// <summary>Lowercase hex SHA-256 of the request body.</summary>
    public const int RequestHashLength = 64;

    /// <summary>
    /// Retention window for a stored response. After this
    /// the middleware treats a hit as a miss and the
    /// retention sweeper hard-deletes the row. The value
    /// is generous on purpose — 24 hours covers any
    /// realistic retry window including client clock skew
    /// and network brownouts.
    /// </summary>
    public static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(24);

    /// <summary>Owner of the key (the user that minted it).</summary>
    public UserId OwnerId { get; private set; } = null!;

    /// <summary>The opaque, user-supplied key string.</summary>
    public IdempotencyKeyValue Key { get; private set; } = null!;

    /// <summary>
    /// SHA-256 of the request body that produced the stored
    /// response. Lowercase hex, 64 chars.
    /// </summary>
    public string RequestHash { get; private set; } = string.Empty;

    /// <summary>
    /// HTTP status code of the original response. Defaults
    /// to 200; the middleware may store any 2xx (or even a
    /// 4xx if the original attempt was a deterministic
    /// failure).
    /// </summary>
    public int ResponseStatusCode { get; private set; } = 200;

    /// <summary>
    /// JSON body of the original response, serialised
    /// verbatim. The middleware returns it on a replay.
    /// </summary>
    public string ResponseJson { get; private set; } = string.Empty;

    /// <summary>
    /// UTC timestamp after which the stored response is
    /// no longer authoritative. Set at record time to
    /// <c>at + RetentionWindow</c>; the middleware treats
    /// a hit past this as a miss and the retention
    /// sweeper hard-deletes the row.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    // EF Core.
    private IdempotencyKey() { }

    private IdempotencyKey(
        IdempotencyKeyId id,
        UserId ownerId,
        IdempotencyKeyValue key,
        string requestHash,
        int responseStatusCode,
        string responseJson,
        DateTimeOffset at)
    {
        Id = id;
        OwnerId = ownerId;
        Key = key;
        RequestHash = requestHash;
        ResponseStatusCode = responseStatusCode;
        ResponseJson = responseJson;
        CreatedAt = at;
        ExpiresAt = at + RetentionWindow;
    }

    /// <summary>
    /// Records a new idempotency key with its first response.
    /// Returns <see cref="Result{T}"/> with a domain error if
    /// the inputs are invalid (bad hash, empty response).
    /// </summary>
    public static Result<IdempotencyKey> Record(
        UserId ownerId,
        IdempotencyKeyValue key,
        string requestHash,
        int responseStatusCode,
        string responseJson,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(requestHash) || requestHash.Length != RequestHashLength)
        {
            return Result.Failure<IdempotencyKey>(DomainError.Validation(
                "idempotency.key.hash_invalid",
                "Request hash must be a 64-character lowercase hex SHA-256 digest."));
        }

        if (responseStatusCode is < 100 or > 599)
        {
            return Result.Failure<IdempotencyKey>(DomainError.Validation(
                "idempotency.key.status_invalid",
                "Response status code must be a valid HTTP status (100-599)."));
        }

        if (responseJson is null)
        {
            return Result.Failure<IdempotencyKey>(DomainError.Validation(
                "idempotency.key.response_required",
                "Response body is required."));
        }

        var record = new IdempotencyKey(
            id: IdempotencyKeyId.New(),
            ownerId: ownerId,
            key: key,
            requestHash: requestHash.ToLowerInvariant(),
            responseStatusCode: responseStatusCode,
            responseJson: responseJson,
            at: at);

        record.AddDomainEvent(new IdempotencyKeyRecorded(
            record.Id, record.Key, record.OwnerId, record.RequestHash, at));
        return Result.Success(record);
    }

    /// <summary>
    /// Creates an in-progress record before the protected effect runs. The
    /// unique owner/key index makes insertion the cross-process election.
    /// </summary>
    public static Result<IdempotencyKey> Reserve(
        UserId ownerId,
        IdempotencyKeyValue key,
        string requestHash,
        DateTimeOffset at)
    {
        Result<IdempotencyKey> result = Record(
            ownerId,
            key,
            requestHash,
            ReservationStatusCode,
            string.Empty,
            at);
        if (result.IsSuccess)
        {
            // A reservation is coordination state, not the completed fact
            // represented by IdempotencyKeyRecorded.
            result.Value.ClearDomainEvents();
            result.Value.ExpiresAt = at + ReservationWindow;
        }
        return result;
    }

    /// <summary>True while the elected caller is still producing a response.</summary>
    public bool IsPending => ResponseStatusCode == ReservationStatusCode;

    /// <summary>Completes a pending reservation without changing its identity.</summary>
    public Result Complete(
        int responseStatusCode,
        string responseJson,
        DateTimeOffset at)
    {
        if (!IsPending)
        {
            return Result.Failure(DomainError.Conflict(
                "idempotency.key.already_completed",
                "The idempotency reservation has already been completed."));
        }

        if (responseStatusCode is < 100 or > 599 or ReservationStatusCode)
        {
            return Result.Failure(DomainError.Validation(
                "idempotency.key.status_invalid",
                "Completed response status must be a valid non-reservation HTTP status."));
        }

        if (responseJson is null)
        {
            return Result.Failure(DomainError.Validation(
                "idempotency.key.response_required",
                "Response body is required."));
        }

        ResponseStatusCode = responseStatusCode;
        ResponseJson = responseJson;
        ExpiresAt = at + RetentionWindow;
        StampChanged(null, at);
        return Result.Success();
    }

    /// <summary>
    /// Returns <c>true</c> when the replayed request body
    /// hash matches the stored one. The middleware uses
    /// this to detect "same key, different payload" abuse.
    /// </summary>
    public bool MatchesRequest(string requestHash) =>
        !string.IsNullOrWhiteSpace(requestHash)
        && string.Equals(RequestHash, requestHash.ToLowerInvariant(), StringComparison.Ordinal);

    /// <summary>
    /// True if the stored response is still authoritative
    /// for replay at <paramref name="now"/>. The
    /// middleware uses this to drop entries past their
    /// <see cref="RetentionWindow"/> instead of returning
    /// a stale response.
    /// </summary>
    public bool IsAlive(DateTimeOffset now) => ExpiresAt > now;
}
