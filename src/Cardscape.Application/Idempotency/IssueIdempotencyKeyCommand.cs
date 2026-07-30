using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Idempotency;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Idempotency;
using Wolverine;

namespace Cardscape.Application.Idempotency;

/// <summary>
/// Records a new <see cref="IdempotencyKey"/> for the calling
/// user with a pre-computed request hash and response JSON.
/// The Application-layer counterpart of the inline
/// <see cref="IdempotencyKey.Record"/> call inside
/// <see cref="IdempotencyKeyMiddleware"/>; it exists so a
/// Wolverine endpoint, a hosted background job, or the REST
/// API can persist a key through the standard command bus
/// without bypassing the unit-of-work.
/// </summary>
/// <param name="Key">The user-supplied idempotency key.</param>
/// <param name="RequestHash">
/// Lowercase hex SHA-256 of the canonicalised request body
/// (see <see cref="RequestHasher"/>).
/// </param>
/// <param name="ResponseStatusCode">
/// HTTP status code of the recorded response (defaults to
/// 200 when not supplied).
/// </param>
/// <param name="ResponseJson">
/// JSON body of the recorded response, serialised verbatim.
/// </param>
public sealed record IssueIdempotencyKeyCommand(
    string Key,
    string RequestHash,
    int ResponseStatusCode,
    string ResponseJson) : IMessage;

/// <summary>
/// Wolverine handler for <see cref="IssueIdempotencyKeyCommand"/>.
/// Persists a fresh <see cref="IdempotencyKey"/> on behalf of
/// the authenticated user, then commits the unit of work.
/// </summary>
public static class IssueIdempotencyKeyCommandHandler
{
    public static async Task<Result<IdempotencyKeyId>> Handle(
        IssueIdempotencyKeyCommand command,
        IIdempotencyKeyStore store,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentUser.IsAuthenticated || currentUser.Id is null)
        {
            return Result.Failure<IdempotencyKeyId>(DomainError.Unauthenticated(
                "idempotency.key.owner_required",
                "An authenticated user is required to issue an idempotency key."));
        }

        var ownerId = currentUser.Id;

        var keyValueResult = IdempotencyKeyValue.Create(command.Key);
        if (keyValueResult.IsFailure)
        {
            return Result.Failure<IdempotencyKeyId>(keyValueResult.Error);
        }

        var recordResult = IdempotencyKey.Record(
            ownerId: ownerId,
            key: keyValueResult.Value,
            requestHash: command.RequestHash,
            responseStatusCode: command.ResponseStatusCode,
            responseJson: command.ResponseJson,
            at: clock.UtcNow);

        if (recordResult.IsFailure)
        {
            return Result.Failure<IdempotencyKeyId>(recordResult.Error);
        }

        await store.AddAsync(recordResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(recordResult.Value.Id);
    }
}
