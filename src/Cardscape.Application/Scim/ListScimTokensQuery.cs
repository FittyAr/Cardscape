using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.Scim;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Scim;

public sealed record ListScimTokensQuery(Guid WorkspaceId) : IMessage;

public static class ListScimTokensQueryHandler
{
    public static async Task<Result<IReadOnlyList<ScimTokenDto>>> Handle(
        ListScimTokensQuery query,
        IRepository<Workspace, WorkspaceId> workspaces,
        IScimTokenRepository tokens,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var workspaceId = new WorkspaceId(query.WorkspaceId);
        Result ownerCheck = await ScimTokenAuthorization.RequireWorkspaceOwnerAsync(
            workspaceId, workspaces, currentUser, ct);
        if (ownerCheck.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ScimTokenDto>>(ownerCheck.Error);
        }

        IReadOnlyList<ScimToken> rows = await tokens.ListForWorkspaceAsync(workspaceId, ct);
        return Result.Success<IReadOnlyList<ScimTokenDto>>(
            rows.Select(ScimTokenDto.FromEntity).ToList());
    }
}
