using System.Text.Json;
using System.Text.Json.Serialization;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Idempotency;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Idempotency;

namespace Cardscape.Mcp.Idempotency;

/// <summary>
/// MCP-side helper for executing a tool handler with
/// at-most-once semantics, driven by a user-supplied
/// <c>idempotencyKey</c>.
///
/// <para>
/// Usage from a tool method:
/// <code>
/// var result = await IdempotentToolRunner.RunAsync(
///     idempotencyKey: idempotencyKey,
///     requestJson: JsonSerializer.Serialize(args),
///     currentUser: currentUser,
///     store: store,
///     clock: clock,
///     handler: async () => await bus.InvokeAsync&lt;...&gt;(..., ct),
///     ct: ct);
/// </code>
/// </para>
///
/// <para>
/// On a hit (the same owner + key was seen before with the
/// same payload hash) the helper short-circuits and returns
/// the stored JSON, deserialised into the handler's return
/// type. On a miss the handler runs, its return value is
/// serialised back to JSON, and the (status, json) pair is
/// recorded before the helper returns.
/// </para>
/// </summary>
public static class IdempotentToolRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Runs <paramref name="handler"/> with idempotency
    /// semantics. The first call with a given
    /// (owner, key) tuple records the response; subsequent
    /// calls with the same key and the same payload hash
    /// return the stored response without re-running the
    /// handler.
    /// </summary>
    /// <param name="idempotencyKey">
    /// User-supplied idempotency key. <c>null</c> or empty
    /// disables idempotency for this call (the handler runs
    /// unconditionally and nothing is stored).
    /// </param>
    /// <param name="requestJson">
    /// Canonical JSON of the request body. The hash of this
    /// string is stored alongside the key and checked on
    /// replay.
    /// </param>
    /// <param name="currentUser">Authenticated principal;
    /// used as the key's owner.</param>
    /// <param name="store">Idempotency key store.</param>
    /// <param name="clock">Wall-clock provider.</param>
    /// <param name="handler">The actual tool body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no user is authenticated.
    /// </exception>
    /// <exception cref="IdempotencyKeyConflictException">
    /// Thrown when the same key is replayed with a different
    /// payload hash.
    /// </exception>
    public static async Task<T> RunAsync<T>(
        string? idempotencyKey,
        string? requestJson,
        ICurrentUser currentUser,
        IIdempotencyKeyStore store,
        IClock clock,
        Func<Task<T>> handler,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return await handler();
        }

        if (!currentUser.IsAuthenticated || currentUser.Id is null)
        {
            throw new InvalidOperationException(
                "MCP idempotent tool call rejected: no authenticated principal.");
        }

        var keyValueResult = IdempotencyKeyValue.Create(idempotencyKey);
        if (keyValueResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"{keyValueResult.Error.Code}: {keyValueResult.Error.Message}");
        }

        var owner = currentUser.Id;
        var key = keyValueResult.Value;
        var requestHash = RequestHasher.Hash(requestJson);

        // Short-circuit on a hit.
        var existing = await store.FindAsync(owner, key, ct);
        if (existing is not null)
        {
            if (!existing.MatchesRequest(requestHash))
            {
                throw new IdempotencyKeyConflictException(existing);
            }
            return Deserialize<T>(existing.ResponseJson);
        }

        // Miss — run the handler and record the response.
        var result = await handler();
        var responseJson = JsonSerializer.Serialize(result, JsonOptions);

        var recordResult = IdempotencyKey.Record(
            ownerId: owner,
            key: key,
            requestHash: requestHash,
            responseStatusCode: 200,
            responseJson: responseJson,
            at: clock.UtcNow);

        if (recordResult.IsSuccess)
        {
            await store.AddAsync(recordResult.Value, ct);
        }

        return result;
    }

    private static T Deserialize<T>(string json)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)json;
        }
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        return value ?? throw new InvalidOperationException(
            "Idempotent replay returned a null result; the original response was likely empty.");
    }
}

/// <summary>
/// Thrown by <see cref="IdempotentToolRunner"/> when the same
/// idempotency key is replayed with a different request
/// payload. Carries the original record so callers can
/// surface the original response status in an error
/// message.
/// </summary>
public sealed class IdempotencyKeyConflictException(IdempotencyKey existing)
    : Exception(
        $"Idempotency key '{existing.Key.Value}' was reused for a different request payload. " +
        "Use a fresh key for each logical operation.")
{
    /// <summary>The original record (with its request hash and response JSON).</summary>
    public IdempotencyKey Existing { get; } = existing;
}
