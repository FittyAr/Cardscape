using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Calendar;
using Cardscape.Application.Cards.Commands;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Cards.Queries;
using Cardscape.Application.Realtime;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

public sealed partial class BoardsTools
{
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
}

