using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Security;
using Wolverine;

namespace Cardscape.Application.Security.Commands;

public sealed record RevokeApiTokenCommand(Guid TokenId, string? Reason) : IMessage;

public static class RevokeApiTokenCommandHandler
{
    public static async Task<Result> Handle(
        RevokeApiTokenCommand command,
        IApiTokenService tokens,
        IApiTokenRepository repository,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        // The user can only revoke their own tokens; the
        // repository call also surfaces the not-found case.
        var existing = await repository.GetByIdAsync(new ApiTokenId(command.TokenId), cancellationToken);
        if (existing is null || existing.UserId.Value != currentUser.Id.Value)
        {
            return Result.Failure(DomainError.NotFound(
                "security.api_token.not_found", "API token was not found."));
        }

        return await tokens.RevokeAsync(existing.Id, command.Reason, cancellationToken);
    }
}
