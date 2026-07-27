using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;
using static Cardscape.Domain.Lists.Errors.ListErrors;

namespace Cardscape.Application.Lists.Queries;

public sealed record GetListQuery(Guid ListId) : IMessage;

public static class GetListQueryHandler
{
    public static async Task<Result<BoardListDto>> Handle(
        GetListQuery query,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardListDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var guard = await MembershipGuards.EnsureCanReadListAsync(
            lists, boards, currentUser.Id.Value, query.ListId, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<BoardListDto>(guard.Error);
        }

        var list = guard.Value.List;
        return Result.Success(new BoardListDto(
            list.Id.Value,
            list.BoardId.Value,
            list.Name.Value,
            list.Position.Value,
            list.IsArchived,
            list.CreatedAt,
            0));
    }
}

public sealed record ListListsForBoardQuery(Guid BoardId, bool IncludeArchived = false)
    : IMessage;

public static class ListListsForBoardQueryHandler
{
    public static async Task<Result<IReadOnlyList<BoardListDto>>> Handle(
        ListListsForBoardQuery query,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardListDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var guard = await MembershipGuards.EnsureCanReadBoardAsync(
            boards, currentUser.Id.Value, query.BoardId, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<BoardListDto>>(guard.Error);
        }

        var items = await lists.ListForBoardAsync(
            new Domain.Boards.BoardId(query.BoardId),
            query.IncludeArchived,
            cancellationToken);

        var rows = items
            .Select(l => new BoardListDto(
                l.Id.Value,
                l.BoardId.Value,
                l.Name.Value,
                l.Position.Value,
                l.IsArchived,
                l.CreatedAt,
                0))
            .ToList();

        return Result.Success<IReadOnlyList<BoardListDto>>(rows);
    }
}
