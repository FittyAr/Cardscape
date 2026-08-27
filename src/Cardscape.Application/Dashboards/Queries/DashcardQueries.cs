using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Dashboards;
using Wolverine;

namespace Cardscape.Application.Dashboards.Queries;

public sealed record ListDashcardsForBoardQuery(Guid BoardId) : IMessage;

public static class ListDashcardsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<DashcardDto>>> Handle(
        ListDashcardsForBoardQuery query,
        IBoardRepository boards,
        IDashboardRepository repo,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<DashcardDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetByIdAsync(new BoardId(query.BoardId), ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<DashcardDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<Dashcard> cards = await repo.ListForBoardAsync(new BoardId(query.BoardId), ct);
        IReadOnlyList<DashcardDto> dtos = cards.Select(DashcardDto.FromEntity).ToList();
        return Result.Success(dtos);
    }
}
