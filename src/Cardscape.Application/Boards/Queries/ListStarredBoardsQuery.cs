using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Application.Boards.Queries;

public sealed record ListStarredBoardsQuery() : IMessage;

public static class ListStarredBoardsQueryHandler
{
    public static async Task<Result<IReadOnlyList<BoardSummaryDto>>> Handle(
        ListStarredBoardsQuery query,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var items = await boards.ListStarredByUserAsync(currentUser.Id.Value, cancellationToken);
        var rows = items
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
