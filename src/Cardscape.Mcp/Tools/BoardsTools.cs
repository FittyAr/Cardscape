using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.Commands;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Boards.Queries;
using Cardscape.Application.Cards.Commands;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Cards.Queries;
using Cardscape.Application.Comments.Commands;
using Cardscape.Application.Comments.DTOs;
using Cardscape.Application.Comments.Queries;
using Cardscape.Application.Labels.Commands;
using Cardscape.Application.Labels.DTOs;
using Cardscape.Application.Labels.Queries;
using Cardscape.Application.Lists.Commands;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Application.Lists.Queries;
using Cardscape.Application.Realtime;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Application.Workspaces.Queries;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Realtime;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface that lets an AI assistant drive a Cardscape
/// board. Every tool goes through the same Application-layer
/// commands and queries the REST API uses, so authorization,
/// validation, and side effects (domain events, audit) all stay
/// in one place. Every mutating tool additionally calls
/// <see cref="IBoardPushClient"/> after success so the live Web
/// UI sees the AI's edit without polling.
/// </summary>
[McpServerToolType]
public sealed class BoardsTools(
    IMessageBus bus,
    ICurrentUser currentUser,
    IBoardPushClient push,
    ICardRepository cards)
{
    // ── Workspaces ─────────────────────────────────────────

    [McpServerTool(Name = "workspaces_list")]
    public async Task<IReadOnlyList<WorkspaceDto>> ListWorkspaces(CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<WorkspaceDto>>>(new ListWorkspacesForUserQuery(), ct);
        return Ensure(result);
    }

    // ── Boards ──────────────────────────────────────────────

    [McpServerTool(Name = "boards_list")]
    public async Task<IReadOnlyList<BoardSummaryDto>> ListBoards(Guid workspaceId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardSummaryDto>>>(
            new ListBoardsForWorkspaceQuery(workspaceId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "boards_get")]
    public async Task<BoardDto> GetBoard(Guid boardId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<BoardDto>>(new GetBoardQuery(boardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "boards_create")]
    public async Task<BoardDto> CreateBoard(
        Guid workspaceId, string name, string? description, int visibility, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<BoardDto>>(
            new CreateBoardCommand(workspaceId, name, description, (Cardscape.Domain.Boards.BoardVisibility)visibility),
            ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "boards_star")]
    public async Task<BoardDto> StarBoard(Guid boardId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<BoardDto>>(new StarBoardCommand(boardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "boards_unstar")]
    public async Task<BoardDto> UnstarBoard(Guid boardId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<BoardDto>>(new UnstarBoardCommand(boardId), ct);
        return Ensure(result);
    }

    // ── Lists ───────────────────────────────────────────────

    [McpServerTool(Name = "lists_list")]
    public async Task<IReadOnlyList<BoardListDto>> ListLists(
        Guid boardId, bool includeArchived, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardListDto>>>(
            new ListListsForBoardQuery(boardId, includeArchived), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "lists_create")]
    public async Task<BoardListDto> CreateList(Guid boardId, string name, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<BoardListDto>>(new CreateListCommand(boardId, name), ct);
        var dto = Ensure(result);
        await push.PushListCreatedAsync(new ListEventPayload(
            dto.Id, dto.BoardId, dto.Name, DateTimeOffset.UtcNow), ct);
        return dto;
    }

    // ── Cards ───────────────────────────────────────────────

    [McpServerTool(Name = "cards_list")]
    public async Task<IReadOnlyList<CardSummaryDto>> ListCards(
        Guid boardId, bool includeArchived, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<CardSummaryDto>>>(
            new ListCardsForBoardQuery(boardId, includeArchived), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "cards_get")]
    public async Task<CardDto> GetCard(Guid cardId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardDto>>(new GetCardQuery(cardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "cards_create")]
    public async Task<CardDto> CreateCard(
        Guid listId, string title, string? description, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardDto>>(
            new CreateCardCommand(listId, title, description), ct);
        var dto = Ensure(result);
        await push.PushCardCreatedAsync(new CardEventPayload(
            dto.Id, Guid.Empty, dto.ListId, dto.Title, DateTimeOffset.UtcNow), ct);
        return dto;
    }

    [McpServerTool(Name = "cards_move")]
    public async Task<CardDto> MoveCard(
        Guid cardId, Guid newListId, double newPosition, CancellationToken ct)
    {
        RequireAuth();
        // The board-resolving server side needs the source list id
        // to compute the from-list; we look it up via the loaded
        // card before invoking the command. This is one cheap
        // repository call on the move hot path.
        var existing = await cards.GetByIdAsync(new CardId(cardId), ct);
        var fromListId = existing?.ListId.Value;
        var result = await bus.InvokeAsync<Result<CardDto>>(
            new MoveCardCommand(cardId, newListId, newPosition), ct);
        var dto = Ensure(result);
        await push.PushCardMovedAsync(new CardMovedPayload(
            dto.Id, Guid.Empty, fromListId ?? dto.ListId, newListId, newPosition,
            DateTimeOffset.UtcNow), ct);
        return dto;
    }

    [McpServerTool(Name = "cards_complete")]
    public async Task<CardDto> CompleteCard(Guid cardId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardDto>>(new CompleteCardCommand(cardId), ct);
        var dto = Ensure(result);
        await push.PushCardCompletedAsync(new CardEventPayload(
            dto.Id, Guid.Empty, dto.ListId, dto.Title, DateTimeOffset.UtcNow), ct);
        return dto;
    }

    [McpServerTool(Name = "cards_reopen")]
    public async Task<CardDto> ReopenCard(Guid cardId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardDto>>(new ReopenCardCommand(cardId), ct);
        var dto = Ensure(result);
        await push.PushCardReopenedAsync(new CardEventPayload(
            dto.Id, Guid.Empty, dto.ListId, dto.Title, DateTimeOffset.UtcNow), ct);
        return dto;
    }

    [McpServerTool(Name = "cards_assign")]
    public async Task<CardDto> AssignCard(Guid cardId, Guid userId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardDto>>(new AssignCardCommand(cardId, userId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "cards_attach_label")]
    public async Task<CardDto> AttachLabel(Guid cardId, Guid labelId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardDto>>(
            new AttachLabelToCardCommand(cardId, labelId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "cards_calendar")]
    public async Task<IReadOnlyList<CalendarEntryDto>> Calendar(
        DateTimeOffset from, DateTimeOffset to, Guid? boardId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<CalendarEntryDto>>>(
            new ListCardsDueInRangeQuery(from, to, boardId), ct);
        return Ensure(result);
    }

    // ── Comments ────────────────────────────────────────────

    [McpServerTool(Name = "comments_add")]
    public async Task<CommentDto> AddComment(Guid cardId, string body, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CommentDto>>(
            new AddCommentCommand(cardId, body), ct);
        var dto = Ensure(result);
        await push.PushCommentAddedAsync(new CommentEventPayload(
            dto.Id, dto.CardId, Guid.Empty, dto.AuthorId, DateTimeOffset.UtcNow), ct);
        return dto;
    }

    [McpServerTool(Name = "comments_list")]
    public async Task<IReadOnlyList<CommentDto>> ListComments(Guid cardId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<CommentDto>>>(
            new ListCommentsForCardQuery(cardId), ct);
        return Ensure(result);
    }

    // ── Labels ──────────────────────────────────────────────

    [McpServerTool(Name = "labels_list")]
    public async Task<IReadOnlyList<LabelDto>> ListLabels(Guid boardId, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<LabelDto>>>(
            new ListLabelsForBoardQuery(boardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "labels_create")]
    public async Task<LabelDto> CreateLabel(Guid boardId, string name, string color, CancellationToken ct)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<LabelDto>>(
            new CreateLabelCommand(boardId, name, color), ct);
        return Ensure(result);
    }

    // ── helpers ──────────────────────────────────────────────

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass a Bearer JWT in the Authorization header on the stdio/stdin transport.");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }

        return result.Value!;
    }
}
