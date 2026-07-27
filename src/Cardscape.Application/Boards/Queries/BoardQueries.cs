using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using MediatR;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Application.Boards.Queries;

public sealed record GetBoardQuery(Guid BoardId) : IRequest<Result<BoardDto>>;

public sealed class GetBoardQueryHandler(
    IBoardRepository boards,
    ICurrentUser currentUser) : IRequestHandler<GetBoardQuery, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        GetBoardQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
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

public sealed record ListBoardsForWorkspaceQuery(Guid WorkspaceId) : IRequest<Result<IReadOnlyList<BoardSummaryDto>>>;

public sealed class ListBoardsForWorkspaceQueryHandler(
    IBoardRepository boards,
    IRepository<Workspace, WorkspaceId> workspaces,
    ICurrentUser currentUser) : IRequestHandler<ListBoardsForWorkspaceQuery, Result<IReadOnlyList<BoardSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<BoardSummaryDto>>> Handle(
        ListBoardsForWorkspaceQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<BoardSummaryDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(request.WorkspaceId), cancellationToken);
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

        var items = await boards.ListForWorkspaceAsync(new WorkspaceId(request.WorkspaceId), cancellationToken);
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

public sealed record ListStarredBoardsQuery() : IRequest<Result<IReadOnlyList<BoardSummaryDto>>>;

public sealed class ListStarredBoardsQueryHandler(
    IBoardRepository boards,
    ICurrentUser currentUser) : IRequestHandler<ListStarredBoardsQuery, Result<IReadOnlyList<BoardSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<BoardSummaryDto>>> Handle(
        ListStarredBoardsQuery request, CancellationToken cancellationToken)
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
