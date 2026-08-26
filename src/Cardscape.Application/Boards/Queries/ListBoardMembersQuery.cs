using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Application.Boards.Queries;

public sealed record ListBoardMembersQuery(Guid BoardId) : IMessage;

public static class ListBoardMembersQueryHandler
{
    public static async Task<Result<IReadOnlyList<BoardMemberDto>>> Handle(
        ListBoardMembersQuery query,
        IBoardRepository boards,
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
            return Result.Failure<IReadOnlyList<BoardMemberDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<BoardMemberDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        // BETA-8-API-#1 - see test-results/r8/r8-report.md.
        // The list endpoint was missing: the add-member endpoint
        // landed in BETA-5-#12 but a corresponding GET did not.
        // We batch-load display names for every distinct user id
        // (no N+1) and project a MemberDto per row. The list is
        // sorted by JoinedAt so the board creator shows first
        // (the domain adds the creator at construction time).
        IReadOnlyList<Domain.Members.UserId> userIds = board.Members
            .Select(m => new Domain.Members.UserId(m.UserId))
            .Distinct()
            .ToList();
        IReadOnlyDictionary<Guid, string> displayNames = (await users.ListByIdsAsync(userIds, cancellationToken))
            .ToDictionary(u => u.Id.Value, u => u.DisplayName.Value);

        IReadOnlyList<BoardMemberDto> rows = board.Members
            .OrderBy(m => m.JoinedAt)
            .Select(m => new BoardMemberDto(
                m.UserId,
                displayNames.GetValueOrDefault(m.UserId, string.Empty),
                m.Role,
                m.JoinedAt))
            .ToList();

        return Result.Success<IReadOnlyList<BoardMemberDto>>(rows);
    }
}
