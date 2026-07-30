using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Idempotency;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Idempotency;
using Cardscape.Domain.Idempotency;

namespace Cardscape.Mcp.Idempotency;

/// <summary>
/// MCP-side facade for <see cref="IdempotencyKeyMiddleware"/>.
/// Kept as a thin shim so existing call sites in
/// <c>Tools/*.cs</c> continue to compile unchanged while the
/// actual idempotency logic now lives in the Application
/// layer (per the §2.4 plan). New code should call
/// <see cref="IdempotencyKeyMiddleware.ExecuteAsync{T}"/>
/// directly.
/// </summary>
public static class IdempotentToolRunner
{
    /// <summary>
    /// Forwards to <see cref="IdempotencyKeyMiddleware.ExecuteAsync{T}"/>.
    /// See that method for the full semantics.
    /// </summary>
    public static Task<T> RunAsync<T>(
        string? idempotencyKey,
        string? requestJson,
        ICurrentUser currentUser,
        IIdempotencyKeyStore store,
        IClock clock,
        Func<Task<T>> handler,
        CancellationToken ct) =>
        IdempotencyKeyMiddleware.ExecuteAsync(
            idempotencyKey: idempotencyKey,
            requestJson: requestJson,
            currentUser: currentUser,
            store: store,
            clock: clock,
            handler: handler,
            ct: ct);
}
