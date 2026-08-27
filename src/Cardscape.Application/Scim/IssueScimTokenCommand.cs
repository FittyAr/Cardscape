using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.Scim;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Scim;

public sealed record IssueScimTokenCommand(Guid WorkspaceId, string Name) : IMessage;

public sealed record IssueScimTokenResult(ScimTokenDto Token, string PlaintextToken);

public static class IssueScimTokenCommandHandler
{
    public static async Task<Result<IssueScimTokenResult>> Handle(
        IssueScimTokenCommand command,
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
            return Result.Failure<IssueScimTokenResult>(ownerCheck.Error);
        }

        var (token, plaintext) = ScimToken.Issue(
            ScimTokenId.New(), workspaceId, command.Name, clock.UtcNow);

        await tokens.AddAsync(token, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new IssueScimTokenResult(ScimTokenDto.FromEntity(token), plaintext));
    }
}
