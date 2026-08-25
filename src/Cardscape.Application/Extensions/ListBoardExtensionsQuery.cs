using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Extensions;

public sealed record ListBoardExtensionsQuery(Guid BoardId) : IMessage;

public static class ListBoardExtensionsQueryHandler
{
    public static async Task<Result<IReadOnlyList<BoardExtensionDto>>> Handle(
        ListBoardExtensionsQuery query,
        IBoardExtensionRepository extensions,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardExtensionDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetWithMembersAsync(
            new BoardId(query.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<IReadOnlyList<BoardExtensionDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<BoardExtensionDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var rows = await extensions.ListForBoardAsync(board.Id, cancellationToken);
        return Result.Success<IReadOnlyList<BoardExtensionDto>>(
            rows.Select(BoardExtensionDto.FromEntity).ToList());
    }
}


