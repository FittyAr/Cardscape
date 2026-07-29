using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Dashboards.Queries;

public sealed record ListDashcardsForBoardQuery(Guid BoardId) : IMessage;

public static class ListDashcardsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<DashcardDto>>> Handle(
        ListDashcardsForBoardQuery query,
        IBoardRepository boards,
        IDashboardRepository dashboards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<DashcardDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(query.BoardId), ct);
        if (board is null)
        {
            return Result.Failure<IReadOnlyList<DashcardDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<DashcardDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var all = await dashboards.ListForBoardAsync(board.Id, ct);
        return Result.Success<IReadOnlyList<DashcardDto>>(
            all
                .OrderBy(d => d.Position)
                .Select(d => new DashcardDto(
                    d.Id.Value,
                    d.BoardId.Value,
                    d.Kind,
                    d.Title,
                    d.ConfigurationJson,
                    d.Position))
                .ToList());
    }
}
