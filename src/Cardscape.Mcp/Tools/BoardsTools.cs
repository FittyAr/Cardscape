using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.Commands;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Boards.Queries;
using Cardscape.Application.Calendar;
using Cardscape.Application.Abstractions.Calendar;
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
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Application.Workspaces.Queries;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
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
    // â”€â”€ Workspaces â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [McpServerTool(Name = "workspaces_list")]
    public async Task<IReadOnlyList<WorkspaceDto>> ListWorkspaces(CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("workspaces_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<WorkspaceDto>>>(new ListWorkspacesForUserQuery(), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // â”€â”€ Boards â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [McpServerTool(Name = "boards_list")]
    public async Task<IReadOnlyList<BoardSummaryDto>> ListBoards(Guid workspaceId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardSummaryDto>>>(
                new ListBoardsForWorkspaceQuery(workspaceId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "boards_get")]
    public async Task<BoardDto> GetBoard(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_get");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardDto>>(new GetBoardQuery(boardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "boards_create")]
    public async Task<BoardDto> CreateBoard(
        Guid workspaceId, string name, string? description, int visibility, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_create");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardDto>>(
                new CreateBoardCommand(workspaceId, name, description, (Cardscape.Domain.Boards.BoardVisibility)visibility),
                ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "boards_star")]
    public async Task<BoardDto> StarBoard(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_star");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardDto>>(new StarBoardCommand(boardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "boards_unstar")]
    public async Task<BoardDto> UnstarBoard(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_unstar");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardDto>>(new UnstarBoardCommand(boardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // â”€â”€ Lists â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [McpServerTool(Name = "lists_list")]
    public async Task<IReadOnlyList<BoardListDto>> ListLists(
        Guid boardId, bool includeArchived, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("lists_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<BoardListDto>>>(
                new ListListsForBoardQuery(boardId, includeArchived), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "lists_create")]
    public async Task<BoardListDto> CreateList(
        Guid boardId,
        string name,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("lists_create");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<BoardListDto>>(
                new CreateListCommand(boardId, name), ct);
            var value = Ensure(result);
            await push.PushListCreatedAsync(new ListEventPayload(
                value.Id, value.BoardId, value.Name, DateTimeOffset.UtcNow), ct);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // â”€â”€ Cards â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [McpServerTool(Name = "cards_list")]
    public async Task<IReadOnlyList<CardSummaryDto>> ListCards(
        Guid boardId, bool includeArchived, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<CardSummaryDto>>>(
                new ListCardsForBoardQuery(boardId, includeArchived), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_get")]
    public async Task<CardDto> GetCard(Guid cardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_get");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardDto>>(new GetCardQuery(cardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_create")]
    public async Task<CardDto> CreateCard(
        Guid listId,
        string title,
        string? description,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_create");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardDto>>(
                new CreateCardCommand(listId, title, description), ct);
            var value = Ensure(result);
            await push.PushCardCreatedAsync(new CardEventPayload(
                value.Id, Guid.Empty, value.ListId, value.Title, DateTimeOffset.UtcNow), ct);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_move")]
    public async Task<CardDto> MoveCard(
        Guid cardId, Guid newListId, double newPosition, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_move");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
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
            __mcpSpan.MarkSuccess();
            return dto;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_complete")]
    public async Task<CardDto> CompleteCard(Guid cardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_complete");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardDto>>(new CompleteCardCommand(cardId), ct);
            var dto = Ensure(result);
            await push.PushCardCompletedAsync(new CardEventPayload(
                dto.Id, Guid.Empty, dto.ListId, dto.Title, DateTimeOffset.UtcNow), ct);
            __mcpSpan.MarkSuccess();
            return dto;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_reopen")]
    public async Task<CardDto> ReopenCard(Guid cardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_reopen");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardDto>>(new ReopenCardCommand(cardId), ct);
            var dto = Ensure(result);
            await push.PushCardReopenedAsync(new CardEventPayload(
                dto.Id, Guid.Empty, dto.ListId, dto.Title, DateTimeOffset.UtcNow), ct);
            __mcpSpan.MarkSuccess();
            return dto;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_archive")]
    public async Task<CardDto> ArchiveCard(Guid cardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_archive");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardDto>>(new ArchiveCardCommand(cardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_restore")]
    public async Task<CardDto> RestoreCard(Guid cardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_restore");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardDto>>(new RestoreCardCommand(cardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_update")]
    public async Task<CardDto> UpdateCard(
        Guid cardId,
        string? newTitle = null,
        string? newDescription = null,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_update");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            Result<CardDto> result = Result<CardDto>.Failure(DomainError.Validation(
                "cards.nothing_to_update", "Provide at least one of newTitle or newDescription."));

            if (!string.IsNullOrWhiteSpace(newTitle))
            {
                result = await bus.InvokeAsync<Result<CardDto>>(new RenameCardCommand(cardId, newTitle), ct);
            }
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(newDescription))
            {
                result = await bus.InvokeAsync<Result<CardDto>>(
                    new ChangeCardDescriptionCommand(cardId, newDescription), ct);
            }
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_assign")]
    public async Task<CardDto> AssignCard(Guid cardId, Guid userId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_assign");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardDto>>(new AssignCardCommand(cardId, userId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_attach_label")]
    public async Task<CardDto> AttachLabel(Guid cardId, Guid labelId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_attach_label");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardDto>>(
                new AttachLabelToCardCommand(cardId, labelId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_calendar")]
    public async Task<IReadOnlyList<CalendarEntryDto>> Calendar(
        DateTimeOffset from, DateTimeOffset to, Guid? boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_calendar");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<CalendarEntryDto>>>(
                new ListCardsDueInRangeQuery(from, to, boardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // â”€â”€ Comments â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [McpServerTool(Name = "comments_add")]
    public async Task<CommentDto> AddComment(Guid cardId, string body, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("comments_add");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CommentDto>>(
                new AddCommentCommand(cardId, body), ct);
            var dto = Ensure(result);
            await push.PushCommentAddedAsync(new CommentEventPayload(
                dto.Id, dto.CardId, Guid.Empty, dto.AuthorId, DateTimeOffset.UtcNow), ct);
            __mcpSpan.MarkSuccess();
            return dto;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "comments_list")]
    public async Task<IReadOnlyList<CommentDto>> ListComments(Guid cardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("comments_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<CommentDto>>>(
                new ListCommentsForCardQuery(cardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // BETA-8-MCP-#2 — see test-results/r8/r8-report.md.
    // comments_add existed but edit/delete were not surfaced as
    // MCP tools, so an AI client could post a comment and then
    // had no way to fix a typo or remove it. Both tools delegate
    // to the existing EditCommentCommand / DeleteCommentCommand
    // handlers so the IDOR fix, search-index re-index, and
    // activity-feed write all stay in one place.
    [McpServerTool(Name = "comments_edit")]
    public async Task<CommentDto> EditComment(Guid commentId, string newBody, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("comments_edit");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CommentDto>>(
                new EditCommentCommand(commentId, newBody), ct);
            var dto = Ensure(result);
            __mcpSpan.MarkSuccess();
            return dto;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "comments_delete")]
    public async Task<object> DeleteComment(Guid commentId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("comments_delete");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result>(
                new DeleteCommentCommand(commentId), ct);
            if (result.IsFailure)
            {
                throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
            }
            __mcpSpan.MarkSuccess();
            return new { ok = true, commentId };
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // â”€â”€ Labels â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [McpServerTool(Name = "labels_list")]
    public async Task<IReadOnlyList<LabelDto>> ListLabels(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("labels_list");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<IReadOnlyList<LabelDto>>>(
                new ListLabelsForBoardQuery(boardId), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "labels_create")]
    public async Task<LabelDto> CreateLabel(Guid boardId, string name, string color, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("labels_create");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<LabelDto>>(
                new CreateLabelCommand(boardId, name, color), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    // â”€â”€ helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass the API token as an Authorization: Bearer header to the MCP HTTP endpoint.");
        }
    }

    [McpServerTool(Name = "boards_get_icalendar")]
    public async Task<string> GetBoardICalendar(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_get_icalendar");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<Stream>>(
                new RenderBoardCalendarQuery(boardId), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
            }
            using var reader = new StreamReader(result.Value);
            var value = await reader.ReadToEndAsync(ct);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "boards_export")]
    public async Task<byte[]> ExportBoard(Guid boardId, CancellationToken ct)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_export");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<Stream>>(
                new ExportBoardQuery(boardId), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
            }
            using var ms = new MemoryStream();
            await result.Value.CopyToAsync(ms, ct);
            var value = ms.ToArray();
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
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
