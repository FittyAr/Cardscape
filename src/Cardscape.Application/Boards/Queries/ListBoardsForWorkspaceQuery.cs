using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Application.Boards.Queries;

public sealed record ListBoardsForWorkspaceQuery(Guid WorkspaceId, bool IncludeArchived = false) : IMessage;

public static class ListBoardsForWorkspaceQueryHandler
{
    public static async Task<Result<IReadOnlyList<BoardSummaryDto>>> Handle(
        ListBoardsForWorkspaceQuery query,
        IBoardRepository boards,
        IRepository<Workspace, WorkspaceId> workspaces,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(query.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<IReadOnlyList<BoardSummaryDto>>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<BoardSummaryDto>>(DomainError.Forbidden(
                "workspaces.not_member", "You are not a member of this workspace."));
        }

        // BETA-A3-R2-005 — see
        // test-results/beta/round-2/reports/A3-boards.md.
        // The default `/api/boards?workspaceId=...` query
        // returned archived boards alongside active ones,
        // so the workspace landing page showed a mix of
        // "Open" and "Archived" tiles without a way to
        // filter. The fix is a single IncludeArchived
        // boolean on the query; the default is `false` so
        // the existing callers (the workspace landing
        // page, the recent-boards list, the search drop)
        // all get the cleaned-up list for free. The board
        // settings page already loads the board directly
        // and the archive toggle renders from there.
        var items = await boards.ListForWorkspaceAsync(new WorkspaceId(query.WorkspaceId), cancellationToken);
        if (!query.IncludeArchived)
        {
            items = items.Where(b => !b.IsArchived).ToList();
        }

        var rows = items
            .Select(b => new BoardSummaryDto(
                b.Id.Value,
                b.Name.Value,
                b.Visibility,
                b.IsArchived,
                b.IsStarredBy(currentUser.Id.Value),
                b.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<BoardSummaryDto>>(rows);
    }
}
