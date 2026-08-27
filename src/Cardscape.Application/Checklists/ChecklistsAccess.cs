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

internal sealed record ChecklistAccessContext(Checklist Checklist, Card Card, BoardId BoardId);

internal static class ChecklistsAccess
{
    public static async Task<Result<ChecklistAccessContext>> EnsureCanAccessChecklistAsync(
        Guid checklistId,
        IChecklistRepository checklists,
        ICardRepository cards,
        IBoardListRepository lists,
        IBoardRepository boards,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<ChecklistAccessContext>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Checklist? checklist = await checklists.GetByIdAsync(new ChecklistId(checklistId), ct);
        if (checklist is null)
        {
            return Result.Failure<ChecklistAccessContext>(DomainError.NotFound(
                "checklists.not_found", "Checklist was not found."));
        }

        Card? card = await cards.GetByIdAsync(checklist.CardId, ct);
        if (card is null)
        {
            return Result.Failure<ChecklistAccessContext>(DomainError.NotFound(
                "cards.not_found", "Card was not found."));
        }

        BoardId? boardId = await lists.GetBoardIdAsync(card.ListId, ct);
        if (boardId is null)
        {
            return Result.Failure<ChecklistAccessContext>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        Board? board = await boards.GetWithMembersAsync(boardId, ct);
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<ChecklistAccessContext>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        return Result.Success(new ChecklistAccessContext(checklist, card, boardId));
    }
}
