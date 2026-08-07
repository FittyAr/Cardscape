using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Application.Boards.Queries;

public sealed record GetBoardQuery(Guid BoardId) : IMessage;

public static class GetBoardQueryHandler
{
    public static async Task<Result<BoardDto>> Handle(
        GetBoardQuery query,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(query.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

        if (!board.IsMember(currentUser.Id.Value) && board.Visibility == BoardVisibility.Private)
        {
            return Result.Failure<BoardDto>(NotMember);
        }

        return Result.Success(new BoardDto(
            board.Id.Value,
            board.WorkspaceId.Value,
            board.Name.Value,
            board.Description.Value,
            board.Visibility,
            board.IsArchived,
            board.IsStarredBy(currentUser.Id.Value),
            board.CreatedAt,
            board.Members.Count));
    }
}

public sealed record ListBoardsForWorkspaceQuery(Guid WorkspaceId) : IMessage;

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

        var items = await boards.ListForWorkspaceAsync(new WorkspaceId(query.WorkspaceId), cancellationToken);
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
