using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using MediatR;
using static Cardscape.Domain.Lists.Errors.ListErrors;

namespace Cardscape.Application.Lists.Queries;

public sealed record GetListQuery(Guid ListId) : IRequest<Result<BoardListDto>>;

public sealed class GetListQueryHandler(
    IBoardListRepository lists,
    ICurrentUser currentUser) : IRequestHandler<GetListQuery, Result<BoardListDto>>
{
    public async Task<Result<BoardListDto>> Handle(
        GetListQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardListDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var list = await lists.GetByIdAsync(new BoardListId(request.ListId), cancellationToken);
        if (list is null)
        {
            return Result.Failure<BoardListDto>(NotFound);
        }

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
    : IRequest<Result<IReadOnlyList<BoardListDto>>>;

public sealed class ListListsForBoardQueryHandler(
    IBoardListRepository lists,
    ICurrentUser currentUser) : IRequestHandler<ListListsForBoardQuery, Result<IReadOnlyList<BoardListDto>>>
{
    public async Task<Result<IReadOnlyList<BoardListDto>>> Handle(
        ListListsForBoardQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardListDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var items = await lists.ListForBoardAsync(
            new Domain.Boards.BoardId(request.BoardId),
            request.IncludeArchived,
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
