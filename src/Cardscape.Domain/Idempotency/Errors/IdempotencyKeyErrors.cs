using Cardscape.Domain.Common;

namespace Cardscape.Domain.Idempotency.Errors;

/// <summary>
/// Domain errors raised by the <see cref="IdempotencyKey"/>
/// aggregate and the request-matching middleware.
/// </summary>
public static class IdempotencyKeyErrors
{
    /// <summary>
    /// Raised when the same idempotency key is replayed with a
    /// different request body hash. The client is asked to use
    /// a fresh key for a different logical request.
    /// </summary>
    public static readonly DomainError KeyReusedWithDifferentPayload = DomainError.Conflict(
        "idempotency.key.payload_mismatch",
        "Idempotency key was reused for a different request payload.");

    /// <summary>
    /// Raised when a previously-recorded response is being
    /// served (short-circuit); the middleware returns the
    /// stored JSON rather than re-running the handler.
    /// </summary>
    public static readonly DomainError AlreadyCompleted = DomainError.Conflict(
        "idempotency.key.already_completed",
        "Idempotency key has already been processed.");
}
