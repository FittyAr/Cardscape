using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Common;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Boards.Queries;

public sealed record ListStarredBoardsQuery() : IMessage;

public static class ListStarredBoardsQueryHandler
{
    public static async Task<Result<IReadOnlyList<BoardSummaryDto>>> HandleAsync(
        ListStarredBoardsQuery query,
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Guid userId = currentUser.Id.Value;
        var items = await boards.ListStarredByUserAsync(userId, cancellationToken);

        // A guest who starred a board and was later removed from it
        // must not keep seeing it.
        HashSet<WorkspaceId> guestOf = await WorkspaceBoardScope.GuestWorkspaceIdsAsync(
            workspaces, userId, cancellationToken);
        var rows = items
            .Where(b => !guestOf.Contains(b.WorkspaceId) || b.IsMember(userId))
            .Select(b => new BoardSummaryDto(
                b.Id.Value,
                b.Name.Value,
                b.Visibility,
                b.IsArchived,
                true,
                b.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<BoardSummaryDto>>(rows);
    }
}
