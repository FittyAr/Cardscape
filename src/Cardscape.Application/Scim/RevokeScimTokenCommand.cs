using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.Scim;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Scim;

public sealed record RevokeScimTokenCommand(Guid WorkspaceId, Guid TokenId) : IMessage;

public static class RevokeScimTokenCommandHandler
{
    public static async Task<Result> Handle(
        RevokeScimTokenCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        IScimTokenRepository tokens,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        var workspaceId = new WorkspaceId(command.WorkspaceId);
        Result ownerCheck = await ScimTokenAuthorization.RequireWorkspaceOwnerAsync(
            workspaceId, workspaces, currentUser, ct);
        if (ownerCheck.IsFailure)
        {
            return ownerCheck;
        }

        ScimToken? token = await tokens.FindByIdAsync(new ScimTokenId(command.TokenId), ct);
        if (token is null || token.WorkspaceId != workspaceId)
        {
            return Result.Failure(DomainError.NotFound(
                "scim.token_not_found", $"SCIM token {command.TokenId} was not found."));
        }

        token.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
