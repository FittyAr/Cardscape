using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Checklists;

public sealed record ChecklistItemDto(
    Guid Id,
    Guid ChecklistId,
    string Text,
    bool IsCompleted,
    int Position,
    Guid? AssignedTo);

public sealed record ChecklistDto(
    Guid Id,
    Guid CardId,
    string Title,
    IReadOnlyList<ChecklistItemDto> Items,
    int CompletedCount,
    int TotalCount)
{
    public static ChecklistDto FromEntity(Checklist c) => new(
        c.Id.Value,
        c.CardId.Value,
        c.Title.Value,
        c.Items
            .OrderBy(i => i.Position.Value)
            .Select(i => new ChecklistItemDto(
                i.Id.Value,
                i.ChecklistId.Value,
                i.Text.Value,
                i.IsCompleted,
                (int)i.Position.Value,
                i.AssignedTo))
            .ToList(),
        CompletedCount: c.Items.Count(i => i.IsCompleted),
        TotalCount: c.Items.Count);
}

public sealed record ListCardChecklistsQuery(Guid CardId) : IMessage;

public static class ListCardChecklistsQueryHandler
{
    public static async Task<Result<IReadOnlyList<ChecklistDto>>> Handle(
        ListCardChecklistsQuery query,
        IChecklistRepository checklists,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IReadOnlyList<ChecklistDto>>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Card? card = await cards.GetByIdAsync(new CardId(query.CardId), ct);
        if (card is null)
        {
            return Result.Failure<IReadOnlyList<ChecklistDto>>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        BoardId? boardId = await lists.GetBoardIdAsync(card.ListId, ct);
        if (boardId is null)
        {
            return Result.Failure<IReadOnlyList<ChecklistDto>>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(boardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<IReadOnlyList<ChecklistDto>>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        IReadOnlyList<Checklist> rows = await checklists.ListForCardAsync(
            card.Id.Value, ct);
        return Result.Success<IReadOnlyList<ChecklistDto>>(
            rows.Select(ChecklistDto.FromEntity).ToList());
    }
}
