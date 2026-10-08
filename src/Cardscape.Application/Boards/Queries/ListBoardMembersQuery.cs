using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Errors;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Boards.Queries;

public sealed record ListBoardMembersQuery(Guid BoardId) : IMessage;

public static class ListBoardMembersQueryHandler
{
    public static async Task<Result<IReadOnlyList<BoardMemberDto>>> HandleAsync(
        ListBoardMembersQuery query,
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardMemberDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(query.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<IReadOnlyList<BoardMemberDto>>(BoardErrors.NotFound);
        }

        // Board members read the roster; so do the people who can
        // manage it without being on the board (workspace managers,
        // instance admins).
        if (!board.IsMember(currentUser.Id.Value)
            && !await BoardMemberAccess.CanManageMembersAsync(
                board,
                await BoardMemberAccess.LoadWorkspaceAsync(board, workspaces, cancellationToken),
                currentUser.Id,
                users,
                cancellationToken))
        {
            return Result.Failure<IReadOnlyList<BoardMemberDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        // Batch-load the users for every distinct id (no N+1). The
        // list is sorted by JoinedAt so the board creator shows first
        // (the domain adds the creator at construction time).
        IReadOnlyList<UserId> userIds = board.Members
            .Select(m => new UserId(m.UserId))
            .Distinct()
            .ToList();
        Dictionary<Guid, User> usersById = (await users.ListByIdsAsync(userIds, cancellationToken))
            .ToDictionary(u => u.Id.Value);

        IReadOnlyList<BoardMemberDto> rows = board.Members
            .OrderBy(m => m.JoinedAt)
            .Select(m =>
            {
                User? user = usersById.GetValueOrDefault(m.UserId);
                return new BoardMemberDto(
                    m.UserId,
                    user?.DisplayName.Value ?? string.Empty,
                    user?.Email.Value,
                    m.Role,
                    m.JoinedAt);
            })
            .ToList();

        return Result.Success(rows);
    }
}
