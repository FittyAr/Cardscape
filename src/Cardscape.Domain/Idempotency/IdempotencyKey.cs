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
/// </remarks>
public sealed class IdempotencyKey : AggregateRoot<IdempotencyKeyId>
{
    /// <summary>Lowercase hex SHA-256 of the request body.</summary>
    public const int RequestHashLength = 64;

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
    /// Returns <c>true</c> when the replayed request body
    /// hash matches the stored one. The middleware uses
    /// this to detect "same key, different payload" abuse.
    /// </summary>
    public bool MatchesRequest(string requestHash) =>
        !string.IsNullOrWhiteSpace(requestHash)
        && string.Equals(RequestHash, requestHash.ToLowerInvariant(), StringComparison.Ordinal);
}
